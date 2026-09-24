param(
    [string] $RepositoryUrl = 'https://github.com/khajiitvaper2017/Comments.git',
    [string] $Branch = 'production',
    [SecureString] $DbPassword,
    [string] $ResourceGroup = 'comments-rg',
    [string] $Location = 'swedencentral',
    [string] $VmName = 'comments-vm',
    [string] $VmSize = 'Standard_D4as_v5',
    [string] $AdminUsername = 'azureuser',
    [int] $FrontendPort = 80,
    [string] $LogPath = './deploy-azure.log'
)

$ErrorActionPreference = 'Stop'

function Invoke-Az {
    param([Parameter(Mandatory)][string[]] $Arguments)

    & az @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI command failed: az $($Arguments -join ' ')"
    }
}

function ConvertTo-Base64([string] $Value) {
    [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Value))
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) { throw 'Azure CLI (az) is required.' }
Invoke-Az @('account', 'show', '--output', 'none')

$vmId = (& az vm show --resource-group $ResourceGroup --name $VmName --query id --output tsv 2>$null) -join ''
$isUpdate = -not [string]::IsNullOrWhiteSpace($vmId.Trim())

if (-not $isUpdate) {
    if ([string]::IsNullOrWhiteSpace($DbPassword)) {
        throw 'DbPassword is required for the first deployment.'
    }

    Invoke-Az @('group', 'create', '--name', $ResourceGroup, '--location', $Location, '--output', 'none')
    Invoke-Az @(
        'vm', 'create', '--resource-group', $ResourceGroup, '--name', $VmName,
        '--image', 'Ubuntu2204', '--size', $VmSize, '--admin-username', $AdminUsername,
        '--public-ip-sku', 'Standard', '--os-disk-size-gb', '128', '--generate-ssh-keys',
        '--output', 'none'
    )
}

Invoke-Az @(
    'vm', 'open-port', '--resource-group', $ResourceGroup, '--name', $VmName,
    '--port', $FrontendPort, '--priority', '1001', '--output', 'none'
)

$repo = ConvertTo-Base64 $RepositoryUrl
$password = ConvertTo-Base64 ($DbPassword ?? '')
$branch = ConvertTo-Base64 $Branch

$setup = @'
#!/bin/sh
set -eu

lock_dir=/tmp/comments-deploy.lock
if ! mkdir "$lock_dir" 2>/dev/null; then
    echo 'Another deployment is already running.' >&2
    exit 1
fi
trap 'rmdir "$lock_dir"' EXIT

log_file=/var/log/comments-deploy.log
: > "$log_file"
echo "===== deployment started $(date -u +%Y-%m-%dT%H:%M:%SZ) ====="

if ! command -v docker >/dev/null 2>&1 || ! docker compose version >/dev/null 2>&1 || ! command -v git >/dev/null 2>&1; then
    sudo apt-get update
    sudo apt-get install -y ca-certificates curl git
    curl -fsSL https://get.docker.com -o /tmp/get-docker.sh
    sudo sh /tmp/get-docker.sh
    rm -f /tmp/get-docker.sh
fi
sudo systemctl enable --now docker
sudo docker info >/dev/null
sudo docker compose version

branch="$(echo __BRANCH__ | base64 -d)"
repository="$(echo __REPOSITORY__ | base64 -d)"
if [ -d /opt/comments-app/.git ]; then
    cd /opt/comments-app
    sudo git fetch --depth 1 origin "$branch"
    sudo git checkout -B "$branch" "origin/$branch"
    sudo git reset --hard "origin/$branch"
else
    sudo rm -rf /opt/comments-app
    sudo git clone --depth 1 --branch "$branch" --single-branch "$repository" /opt/comments-app
fi
cd /opt/comments-app

provided_password="$(echo __PASSWORD__ | base64 -d)"
if [ -n "$provided_password" ]; then
    db_password="$provided_password"
else
    db_password="$(sed -n 's/^COMMENTS_DB_PASSWORD=//p' .env 2>/dev/null || true)"
fi
if [ -z "$db_password" ]; then
    echo 'COMMENTS_DB_PASSWORD is missing from the existing .env file.' >&2
    exit 1
fi

sudo tee .env >/dev/null <<ENV
ASPNETCORE_ENVIRONMENT=Production
COMMENTS_DB_PASSWORD=$db_password
COMMENTS_ENABLE_LOADTEST_CAPTCHA=false
COMMENTS_SWAGGER_ENABLED=false
COMMENTS_STORAGE_ROOT=/data/uploads
COMMENTS_HTTP_PORT=__PORT__
ENV
sudo chmod 600 .env

export DOCKER_BUILDKIT=1
export COMPOSE_DOCKER_CLI_BUILD=1
sudo -E docker compose build
sudo docker compose up -d
sudo docker compose ps
echo '__COMMENTS_DEPLOYMENT_SUCCEEDED__'
'@

$replacements = @{
    '__REPOSITORY__' = $repo
    '__PASSWORD__'   = $password
    '__BRANCH__'     = $branch
    '__PORT__'       = $FrontendPort.ToString()
}
foreach ($replacement in $replacements.GetEnumerator()) {
    $setup = $setup.Replace($replacement.Key, $replacement.Value)
}

$publicIp = (& az vm show --resource-group $ResourceGroup --name $VmName --show-details --query publicIps --output tsv) -join ''

if ([string]::IsNullOrWhiteSpace($publicIp)) {
    throw 'The VM has no public IP address.'
}

if (-not (Get-Command ssh -ErrorAction SilentlyContinue) -or
    -not (Get-Command scp -ErrorAction SilentlyContinue)) {
    throw 'OpenSSH client (ssh and scp) is required.'
}

$sshKey = Join-Path $env:USERPROFILE '.ssh\id_rsa'
if (-not (Test-Path $sshKey)) {
    throw "SSH key not found: $sshKey"
}

$temporaryScript = Join-Path ([IO.Path]::GetTempPath()) "comments-deploy-$([guid]::NewGuid()).sh"
$remoteScriptPath = "/tmp/comments-deploy-$([guid]::NewGuid()).sh"
$setup = $setup.Replace("`r`n", "`n")
[IO.File]::WriteAllText($temporaryScript, $setup, [Text.UTF8Encoding]::new($false))

$sshOptions = @('-i', $sshKey, '-o', 'StrictHostKeyChecking=accept-new', '-o', 'ConnectTimeout=30')
try {
    $target = "${AdminUsername}@${publicIp}:$remoteScriptPath"
    $copyOutput = (& scp @sshOptions $temporaryScript $target 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) {
        $copyOutput | Set-Content -Path $LogPath -Encoding utf8
        throw "Could not copy the deployment script to the VM.`n$copyOutput"
    }

    & ssh @sshOptions "${AdminUsername}@${publicIp}" "sudo sh $remoteScriptPath 2>&1 | sudo tee -a /var/log/comments-deploy.log" 2>&1 |
        Tee-Object -FilePath $LogPath
    $sshExitCode = $LASTEXITCODE
    Write-Host "Deployment log saved to $LogPath"

    $remoteText = Get-Content -Path $LogPath -Raw
    if ($sshExitCode -ne 0 -or $remoteText -notmatch '__COMMENTS_DEPLOYMENT_SUCCEEDED__') {
        Write-Error $remoteText
        throw 'Remote deployment failed. Check the deployment log.'
    }
}
finally {
    Remove-Item $temporaryScript -Force -ErrorAction SilentlyContinue
}

$action = if ($isUpdate) { 'Update' } else { 'Initial deployment' }
Write-Host "$action complete: http://$publicIp`:$FrontendPort"
