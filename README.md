# Comments

Comments is a single-page application, created with Angular and ASP.NET Core. It also uses SQL Server, Redis, RabbitMQ, GraphQL and Elasticsearch. The application is designed to be deployed in Docker containers.
Users can post comments, reply to existing comments, format text and attach files. The Angular single-page frontend communicates with an ASP.NET Core API, which stores data in SQL Server through Entity Framework Core.

This application is organized into separate Domain, Application, Infrastructure, API and Frontend projects.

## Run with Docker

Requirements: Docker Desktop.

Run PowerShell in root folder, set your database password in the `.env` file, and start the application, for example:

```powershell
Copy-Item .env.development.example .env
docker compose up --build
```

`COMMENTS_DB_PASSWORD` must be at least 8 characters long and contain characters from at least three of these four groups: uppercase letters, lowercase letters, digits, and symbols.

Open [http://localhost:8080](http://localhost:8080).

Docker runs SQL Server, Redis, RabbitMQ, Elasticsearch, the ASP.NET API, and Angular separately. Nginx serves the Angular frontend and proxies `/api`, `/graphql`, and SignalR requests to the API. Docker stores the database in the `comments-db` volume, uploaded files in the `comments-files` volume, and the Elasticsearch index in the `comments-search` volume.

The API applies database migrations on startup.

## Deploy to Azure VM

Run from this directory:

```powershell
az login
az account set --subscription <subscription-id>
.\deploy-azure.ps1 -DbPassword '<database-password>'
```

You may need to check and change the deployment region and VmSize in the script. 

For later deployments:

```powershell
.\deploy-azure.ps1
```

## API
The API is exposed through the same host as the frontend. In Docker, its base URL is `http://localhost:8080`. JSON property names are camel-cased. Interactive Swagger documentation is available at [http://localhost:8080/api/](http://localhost:8080/api/) when Swagger is enabled; its OpenAPI document is `/api/v1.json`.

### REST

| Method | Endpoint | Parameters / result |
| --- | --- | --- |
| `GET` | `/api/comments` | Root-comment page. Query: `sort` (`createdAt`, `userName`, or `email`; defaults to `createdAt`), `descending` (defaults to `true`), and optional `cursor`. |
| `GET` | `/api/comments/{parentId}/replies` | Up to 24 direct replies, with descendants included for small reply trees. |
| `GET` | `/api/comments/ancestors` | Requested ancestor comments in input order. Repeat `ids`, for example `?ids=<id1>&ids=<id2>`. |
| `POST` | `/api/comments` | Creates a root comment or reply from `multipart/form-data`. |
| `GET` | `/api/captcha` | Creates a single-use CAPTCHA challenge that expires after five minutes. |
| `GET` | `/api/attachments/{id}` | Serves an image inline; downloads a text attachment. |
| `GET` | `/api/search` | Elasticsearch search page. See search parameters below. |
| `GET` | `/health` | Health-check endpoint. |

`GET /api/comments` returns at most 25 root comments. Send the returned `nextCursor` as the next request's `cursor`. An invalid sort is treated as `createdAt`, and a cursor that does not match the requested sort/direction is ignored.

Search accepts a required `q` plus these optional flags: `partial=false`, `searchText=true`, `searchUserName=true`, `searchComments=true`, `searchReplies=true`, and `cursor`.

SignalR clients connect to `/hubs/discussions` and receive a `commentChanged` event with a comment payload after asynchronous processing.

### GraphQL

GraphQL is a read-only endpoint at `POST /graphq`. Its query fields mirror the REST reads:

| Field | Arguments |
| --- | --- |
| `comments` | `sort: String = "createdAt"`, `descending: Boolean = true`, `cursor: String` |
| `replies` | `parentId: UUID!` |
| `ancestors` | `ids: [UUID!]!` |
| `search` | `query: String!`, `partial: Boolean = false`, `searchText: Boolean = true`, `searchUserName: Boolean = true`, `searchComments: Boolean = true`, `searchReplies: Boolean = true`, `cursor: String` |

## Features

- **Comments and replies** — create root comments and nested replies.
- **Two display modes** — browse discussions as a threaded card view or a sortable table.
- **Cursor continuation and sorting** — root comments are loaded in slices of 25 and can be sorted by user name, e-mail, or date. App uses cursor continuation to load the next slice of comments. Replies are loaded on demand for each comment unless has small amount of descendants, in which case the entire tree is loaded.
- **Text formatting** — edit HTML-like markup with toolbar actions for `a`, `code`, `i` and `strong`, or switch to a rendered preview.
- **Validation and CAPTCHA** — input is validated in the browser and on the server before a comment is saved. Captcha challenges prevent automated spam. The CAPTCHA is bypassed in load tests.
- **File attachments** — upload JPG, JPEG, GIF, PNG, or TXT files; images open in a zoomable lightbox and text files open in a scrollable preview. Images and GIFs are converted to a webp format, originals are deleted.
- **Security** — comment HTML is sanitized and security headers protect the served application.
- **SQL Server** — SQL Server stores comments and attachment metadata; EF Core migrations create and update the schema.
- **Separate file storage** — uploaded files are stored outside the API binaries, using a Docker volume.
- **Search** — search comment text and user names through Elasticsearch, with cursor continuation and highlighting of matching terms.
- **Asynchronous processing and search** — RabbitMQ workers process image attachments and update the Elasticsearch index after comment creation; Redis caches cursor slices and SignalR broadcasts updates.

## Useful commands

Run the focused automated tests:

```powershell
dotnet test
npx --prefix .\Comments.Frontend ng test --watch=false --no-progress
```

Run the fixed NBomber load scenario against a running Docker stack:

```powershell
$env:COMMENTS_ENABLE_LOADTEST_CAPTCHA = 'true'
docker compose up --build -d
dotnet run --project .\Comments.Tests\LoadTests\Comments.LoadTests.csproj
```

The scenario is stored in `Comments.Tests/LoadTests` and runs for 1 minute at
approximately 12 comment writes per second. This models the required
1,000,000 messages per 24 hours without making the test itself run for 24 hours.
The read scenarios are included.
The runner performs both reads and writes. Use a disposable database. 
The CAPTCHA bypass is enabled only by `COMMENTS_ENABLE_LOADTEST_CAPTCHA=true` and
defaults to disabled.

Building the solution and frontend:

```powershell
dotnet build .\Comments.slnx

cd .\Comments.Frontend
npm run format:check
npm run lint
npm run build
```

`schema.sql` is a MySQL-compatible schema script for replicating the database in MySQL. It is available at `mysql` branch.
