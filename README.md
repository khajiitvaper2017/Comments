# Comments

Comments is a single-page application, created with Angular and ASP.NET Core. It also uses SQL Server, Redis, RabbitMQ, GraphQL and Elasticsearch. The application is designed to be deployed in Docker containers.
Users can post comments, reply to existing comments, format text and attach files. The Angular single-page frontend communicates with an ASP.NET Core API, which stores data in SQL Server through Entity Framework Core.

This application is organized into separate Domain, Application, Infrastructure, API and Frontend projects.

## Run with Docker

Requirements: Docker Desktop.

Run PowerShell in root folder, set your database password and start the application, for example:

```powershell
Copy-Item .env.development.example .env
docker compose up --build
```

`COMMENTS_DB_PASSWORD` must be at least 8 characters long and contain characters from at least three of these four groups: uppercase letters, lowercase letters, digits, and symbols.

Open [http://localhost:8080](http://localhost:8080).

Docker runs SQL Server, Redis, RabbitMQ, Elasticsearch, the ASP.NET API, and Angular separately. Nginx serves the Angular frontend and proxies `/api`, `/graphql`, and SignalR requests to the API. Docker stores the database in the `comments-db` volume, uploaded files in the `comments-files` volume, and the Elasticsearch index in the `comments-search` volume.

## API
Interactive Swagger documentation is available at [http://localhost:8080/api/](http://localhost:8080/api/). The OpenAPI document is available at `/api/v1.json`.

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/api/comments?sort=createdAt&descending=true` | Get a bounded root slice. Pass the returned `nextCursor` as `cursor` to continue. Supported sorting fields are `createdAt`, `userName`, and `email`. |
| `GET` | `/api/comments/{parentId}/replies` | Load direct replies and permitted small descendant trees for one comment. |
| `GET` | `/api/comments/ancestors?ids={id1}&ids={id2}` | Load specific ancestor comments for a search result. |
| `POST` | `/api/comments` | Create a comment or reply. Accepts multipart form data, including optional attachments and CAPTCHA fields. |
| `GET` | `/api/captcha` | Create a CAPTCHA challenge. |
| `GET` | `/api/attachments/{id}` | Display or download an uploaded attachment. |
| `GET` | `/api/search?q=term` | Search sanitized comment text and user names through Elasticsearch. Pass the returned `nextCursor` as `cursor` to continue. |
| `GET` | `/health` | Check whether the API is running. |

SignalR clients connect to `/hubs/discussions`.

### GraphQL

GraphQL is available at `POST /graphql` and exposes read-only operations:

| Operation | Purpose |
| --- | --- |
| `comments(sort, descending, cursor)` | Load bounded root comment slices. Use the returned `nextCursor` to continue. |
| `search(query, partial, searchText, searchUserName, cursor)` | Search comments through Elasticsearch with cursor continuation. |
| `replies(parentId)` | Load replies for one comment. |
| `ancestors(ids)` | Load specific ancestors for a search result. |

The Angular queries are defined in `Comments.Frontend/src/app/core/graphql/comment-queries.ts`.

The API applies database migrations on startup. Invalid input returns an HTTP `400` response with an `error` message; unexpected server errors return HTTP `500`.

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

`schema.sql` is a MySQL-compatible schema script for replicating the database in MySQL. It is maintained separately from the SQL Server EF Core migrations used by the application.
