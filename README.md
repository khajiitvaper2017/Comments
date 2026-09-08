# Comments SPA

ASP.NET Core 10 + Angular 22 single-page comments application backed by SQL Server and EF Core.

## Run with Docker

From this directory:

```sh
docker compose up --build
```

Set a local password in your shell before starting Docker Compose, for example `COMMENTS_DB_PASSWORD='use-a-local-password-here' docker compose up --build`

PowerShell: `$env:COMMENTS_DB_PASSWORD='use-a-local-password-here'; docker compose up --build`

Open `http://localhost:8080`. Uploaded files are stored in the `comments-files` volume.

## Run locally with SQL Server and SSMS

SSMS is the management client; a SQL Server engine must also be installed and running. Open SSMS and confirm you can connect to your instance first. Common server names are `localhost`, `localhost\\SQLEXPRESS`, or `(localdb)\\MSSQLLocalDB`.

From PowerShell in this directory, configure the connection string:

```powershell
dotnet user-secrets --project .\Comments.Api set "ConnectionStrings:Comments" "Server=localhost;Database=Comments;Trusted_Connection=True;TrustServerCertificate=True"
```

Create the database schema with the checked-in migration:

```powershell
dotnet ef database update --project .\Comments.Infrastructure --startup-project .\Comments.Api
```

The API also applies pending migrations at startup. Start it with:

```powershell
dotnet run --project .\Comments.Api
```

In a second PowerShell window, start Angular:

```powershell
cd .\Comments.Frontend
npm install
npm start
```

Open the HTTPS URL printed by Angular, normally `https://localhost:52050`. The database should now be visible in SSMS under the `Comments` database. Local uploads are stored outside the API project in `Comments.Storage/uploads`; Docker stores them in the separate `comments-files` volume mounted at `/data`. The checked-in `schema.sql` is available when a DBA-reviewed SQL script is preferred.

After changing the C# model and adding a migration, regenerate the checked-in SQL schema artifact with:

```powershell
dotnet ef migrations script --project .\Comments.Infrastructure --startup-project .\Comments.Api --output .\schema.sql
```

## Verification

```sh
dotnet build Comments.slnx
cd Comments.Frontend && npm run build
```

The API exposes `GET /api/captcha`, `GET /api/comments`, `POST /api/comments` (multipart form), and `GET /api/attachments/{id}`. Comment HTML is allow-listed and sanitized server-side; filenames are never used as storage paths.
