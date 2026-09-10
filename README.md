# Comments

Comments is a single-page application. 
Users can post comments, reply to existing comments, format text and attach files. The Angular single-page frontend communicates with an ASP.NET Core API, which stores data in SQL Server through Entity Framework Core.

This application is organized into separate Domain, Application, Infrastructure, API and Frontend projects.

## Run with Docker

Requirements: Docker Desktop.

Run PowerShell in root folder, set the database password and start the application:

```powershell
$env:COMMENTS_DB_PASSWORD = 'Comments-Db_2026!'
docker compose up --build
```

`COMMENTS_DB_PASSWORD` must be at least 8 characters long and contain characters from at least three of these four groups: uppercase letters, lowercase letters, digits, and symbols.

Open [http://localhost:8080](http://localhost:8080).

Docker runs SQL Server, the ASP.NET API, and Angular separately. Nginx serves the Angular frontend and proxies `/api` requests to the API. Docker stores the database in the `comments-db` volume and uploaded files in the `comments-files` volume. Stop the application with `Ctrl+C`.

## Run locally

Requirements:

- .NET 10 SDK
- Node.js and npm
- SQL Server (SQL Server Express, LocalDB, or another instance)

SSMS is only a database client. A SQL Server engine must also be installed and running.

### 1. Configure SQL Server

From the project directory, set the connection string. Change the server name if necessary:

```powershell
dotnet user-secrets --project .\Comments.Api set `
  "ConnectionStrings:Comments" `
  "Server=localhost\SQLEXPRESS;Database=Comments;Trusted_Connection=True;TrustServerCertificate=True"
```

For LocalDB, use `Server=(localdb)\MSSQLLocalDB` instead.

### 2. Start the API

```powershell
dotnet run --project .\Comments.Api
```

The API also applies pending migrations when it starts.

### 3. Start Angular

In a second terminal:

```powershell
cd .\Comments.Frontend
npm ci
npm start
```

Open the HTTPS address printed by Angular, usually `https://localhost:52050`.

Local uploaded files are stored in `Comments.Storage/uploads`.

## API

The ASP.NET Core API is served by the `Comments.Api` project. The Angular frontend uses the `/api` endpoints below:

Interactive Swagger documentation is available at [http://localhost:8080/api/](http://localhost:8080/api/). The OpenAPI document is available at `/api/v1.json`.

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/api/comments?page=1&sort=createdAt&descending=true` | Get a page of root comments. Supported sorting fields are `createdAt`, `userName`, and `email`. |
| `POST` | `/api/comments` | Create a comment or reply. Accepts multipart form data, including optional attachments and CAPTCHA fields. |
| `GET` | `/api/captcha` | Create a CAPTCHA challenge. |
| `GET` | `/api/attachments/{id}` | Display or download an uploaded attachment. |
| `GET` | `/health` | Check whether the API is running. |

The API applies database migrations on startup. Invalid input returns an HTTP `400` response with an `error` message; unexpected server errors return HTTP `500`.

## Features

- **Comments and replies** — create root comments and nested replies.
- **Two display modes** — browse discussions as a threaded card view or a sortable table.
- **Pagination and sorting** — root comments are loaded in pages and can be sorted by user name, e-mail, or date.
- **Text formatting** — edit HTML-like markup with toolbar actions for `a`, `code`, `i` and `strong`, or switch to a rendered preview.
- **Validation and CAPTCHA** — input is validated in the browser and on the server before a comment is saved.
- **File attachments** — upload JPG, JPEG, GIF, PNG, or TXT files; images open in a zoomable lightbox and text files open in a scrollable preview.
- **Security** — comment HTML is allow-listed and sanitized on the server; security headers protect the served application.
- **Persistence** — SQL Server stores comments and attachment metadata; EF Core migrations create and update the schema.
- **Separate file storage** — uploaded files are stored outside the API binaries, using local storage or a Docker volume.

## Useful commands

```powershell
dotnet build .\Comments.slnx

cd .\Comments.Frontend
npm run format:check
npm run lint
npm run build
```

`schema.sql` is a MySQL-compatible schema script for replicating the database in MySQL. It is maintained separately from the SQL Server EF Core migrations used by the application.