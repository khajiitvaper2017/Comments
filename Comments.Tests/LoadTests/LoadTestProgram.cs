using System.Net.Http.Headers;
using System.Text.Json;
using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;

const string BaseUrl = "http://localhost:8080";
const int TestDurationMinutes = 1;
const int TargetUsersPerDay = 100_000;
const int writeRatePerSecond = 12; // 1,000,000 messages / 86,400 seconds, rounded normally
string[] sortFields = ["createdAt", "userName", "email"];
string[] searchTerms = ["they", "big", "her", "him"];
var testAttachments = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "TestAttachments"))
    .Select(path => new
    {
        FileName = Path.GetFileName(path),
        ContentType = GetContentType(path),
        Content = File.ReadAllBytes(path)
    })
    .ToArray();

if (testAttachments.Length == 0)
    throw new InvalidOperationException("No load-test attachments were found.");

using var httpClient = Http.CreateDefaultClient();
httpClient.BaseAddress = new Uri(BaseUrl);
var parentIds = await FindParentIds(httpClient);

// 60 root-page reads/s
var readComments = Scenario.Create("read_comments", async context =>
    {
        var page = context.InvocationNumber % 1000 + 1;
        var sort = sortFields[context.InvocationNumber % sortFields.Length];
        var descending = context.InvocationNumber % 2 == 0;
        var request = Http.CreateRequest("GET",
                $"/api/comments?page={page}&sort={sort}&descending={descending.ToString().ToLowerInvariant()}")
            .WithHeader("Accept", "application/json");
        return await Http.Send(httpClient, request);
    })
    .WithWarmUpDuration(TimeSpan.FromSeconds(5))
    .WithLoadSimulations(Inject(60));

// 20 reply reads/s
var readReplies = Scenario.Create("read_replies", async context =>
    {
        var parentId = parentIds[context.InvocationNumber % parentIds.Length];
        var request = Http.CreateRequest("GET", $"/api/comments/{parentId}/replies")
            .WithHeader("Accept", "application/json");
        return await Http.Send(httpClient, request);
    })
    .WithWarmUpDuration(TimeSpan.FromSeconds(5))
    .WithLoadSimulations(Inject(20));

// 10 searches/s
var searchComments = Scenario.Create("search_comments", async context =>
    {
        var page = context.InvocationNumber % 3 + 1;
        var term = searchTerms[context.InvocationNumber % searchTerms.Length];
        var request = Http.CreateRequest("GET", $"/api/search?q={term}&page={page}")
            .WithHeader("Accept", "application/json");
        return await Http.Send(httpClient, request);
    })
    .WithWarmUpDuration(TimeSpan.FromSeconds(5))
    .WithLoadSimulations(Inject(10));

var writeComments = Scenario.Create("write_comments", async context =>
    {
        var userNumber = context.InvocationNumber % TargetUsersPerDay + 1;
        var userName = $"LoadUser{userNumber}";
        using var form = new MultipartFormDataContent
        {
            { new StringContent(userName), "userName" },
            { new StringContent($"{userName}@loadtest.example"), "email" },
            { new StringContent($"Load test comment {context.InvocationNumber}."), "text" },
            { new StringContent("load-test"), "captchaId" },
            { new StringContent("bypass"), "captchaAnswer" }
        };
        var testAttachment = testAttachments[context.InvocationNumber % testAttachments.Length];
        var attachment = new ByteArrayContent(testAttachment.Content);
        attachment.Headers.ContentType = new MediaTypeHeaderValue(testAttachment.ContentType);
        form.Add(attachment, "attachments", testAttachment.FileName);

        return await Http.Send(httpClient, Http.CreateRequest("POST", "/api/comments").WithBody(form));
    })
    .WithWarmUpDuration(TimeSpan.FromSeconds(5))
    .WithLoadSimulations(Inject(writeRatePerSecond));

NBomberRunner
    .RegisterScenarios(readComments, readReplies, searchComments, writeComments)
    .WithTestSuite("Comments API")
    .WithTestName("one-million-messages-per-day")
    .WithReportFolder(Path.Combine("Comments.Tests", "LoadTests", "reports"))
    .WithReportFileName("comments-load-test")
    .Run();

static LoadSimulation Inject(int rate)
{
    return Simulation.Inject(rate, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(TestDurationMinutes));
}

static string GetContentType(string path)
{
    return Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".txt" => "text/plain",
        _ => "application/octet-stream"
    };
}

static async Task<Guid[]> FindParentIds(HttpClient client)
{
    var ids = new List<Guid>();
    for (var page = 1; page <= 20; page++)
    {
        using var response = await client.GetAsync(
            $"/api/comments?page={page}&sort=createdAt&descending=true");
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var items = document.RootElement.GetProperty("items");
        foreach (var item in items.EnumerateArray())
            ids.Add(item.GetProperty("id").GetGuid());

        if (items.GetArrayLength() == 0)
            break;
    }

    return ids.Count > 0
        ? ids.ToArray()
        : throw new InvalidOperationException("The API has no comments to use for reply load testing.");
}
