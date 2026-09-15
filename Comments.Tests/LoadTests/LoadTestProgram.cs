using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http;
using NBomber.Http.CSharp;

const string BaseUrl = "http://localhost:8080";

const int TestDurationMinutes = 1;

const int TargetUsersPerDay = 100_000;
const int RootReadRatePerSecond = 60;
const int ReplyReadRatePerSecond = 20;
const int SearchRatePerSecond = 10;
const int WarmUpSeconds = 5;
const int ReplyWriteRatePerSecond = 2; // 2 replies per second
const int rootWriteRatePerSecond = 10; // 10 root comments per second
const int AttachmentEveryNMessages = 10;
const int AncestorReadRatePerSecond = 5;
const int AttachmentReadRatePerSecond = 5;

string[] sortFields = ["createdAt", "userName", "email"];
string[] searchTerms =
[
    "the", "and", "chapter", "comment", "reply", "king", "queen", "they", "her", "him",
    "load", "user", "story", "book", "world", "time", "life", "people", "place", "house",
    "night", "day", "man", "woman", "child", "family", "friend", "work", "way", "thing",
    "good", "great", "new", "old", "first", "last", "long", "little", "large", "small",
    "high", "low", "right", "left", "next", "previous", "early", "late", "young", "best",
    "better", "make", "made", "find", "found", "take", "give", "get", "go", "come",
    "see", "know", "think", "look", "want", "need", "use", "tell", "ask", "feel",
    "seem", "become", "leave", "keep", "put", "bring", "begin", "start", "end", "turn",
    "move", "live", "happen", "show", "hear", "play", "run", "write", "read", "speak",
    "mean", "help", "try", "call", "change", "follow", "stop", "hold", "set", "learn",
    "remember", "believe", "understand", "name", "line", "word", "part", "point", "case", "group",
    "problem", "question", "answer", "water", "fire", "earth", "air", "light", "dark", "sun",
    "moon", "star", "sky", "sea", "tree", "road", "town", "city", "country", "school",
    "room", "door", "window", "food", "heart", "mind", "hand", "head", "face", "voice",
    "green", "red", "blue", "white", "black", "gold", "silver", "true", "false", "free",
    "open", "close", "full", "empty", "hard", "easy", "strong", "weak", "clear", "public",
    "possible", "simple", "important", "local", "current", "search", "result", "page", "number", "text",
    "email", "username", "attachment", "image", "file", "data", "cache", "server", "database", "query",
    "index", "message", "event", "queue", "api", "request", "response", "error", "success", "test"
];
var searchVariants = new[]
{
    // Exercise every valid combination of exact/partial matching and search fields.
    new SearchVariant(false, true, true),
    new SearchVariant(true, true, true),
    new SearchVariant(false, true, false),
    new SearchVariant(true, true, false),
    new SearchVariant(false, false, true),
    new SearchVariant(true, false, true)
};
var rootContinuations = new ConcurrentBag<RootContinuation>();
var searchContinuations = new ConcurrentDictionary<string, ConcurrentBag<string>>();
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
var seed = await LoadSeedData(httpClient, searchTerms);
var reportFolder = Path.Combine(
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "reports")),
    DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss"));

// 60 bounded root reads/s. Once a response exposes a cursor, later invocations
// randomly reuse one of the discovered continuations, exercising arbitrary slices
// without crawling the whole database during test startup.
var readComments = Scenario.Create("read_comments", async context =>
    {
        var continuation = TakeRandom(rootContinuations);
        var sort = continuation?.Sort ?? sortFields[context.InvocationNumber % sortFields.Length];
        var descending = continuation?.Descending ?? context.InvocationNumber % 2 == 0;
        var cursor = continuation?.Cursor;
        var cursorQuery = cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}";
        var request = Http.CreateRequest("GET",
                $"/api/comments?sort={sort}&descending={descending.ToString().ToLowerInvariant()}{cursorQuery}")
            .WithHeader("Accept", "application/json");
        var response = await Http.Send<CursorPageResponse>(httpClient, request);
        AddRootContinuation(response, rootContinuations, sort, descending);
        return response;
    })
    .WithWarmUpDuration(TimeSpan.FromSeconds(WarmUpSeconds))
    .WithLoadSimulations(Inject(RootReadRatePerSecond));

// 20 reply reads/s.
var readReplies = Scenario.Create("read_replies", async context =>
    {
        var parentId = seed.ParentIds[context.InvocationNumber % seed.ParentIds.Length];
        var request = Http.CreateRequest("GET", $"/api/comments/{parentId}/replies")
            .WithHeader("Accept", "application/json");
        return await Http.Send(httpClient, request);
    })
    .WithWarmUpDuration(TimeSpan.FromSeconds(WarmUpSeconds))
    .WithLoadSimulations(Inject(ReplyReadRatePerSecond));

// 10 searches/s. Each search variant keeps at most five discovered cursors.
// Requests therefore exercise the first result plus a random continuation from
// the next five result slices, without wandering arbitrarily deep into results.
var searchComments = Scenario.Create("search_comments", async context =>
    {
        var term = searchTerms[context.InvocationNumber % searchTerms.Length];
        var variant = searchVariants[context.InvocationNumber % searchVariants.Length];
        var searchKey = $"{term}:{variant.Partial}:{variant.SearchText}:{variant.SearchUserName}";
        var cursorPool = searchContinuations.GetOrAdd(searchKey, _ => new ConcurrentBag<string>());
        var cursor = TakeRandomLimited(cursorPool, 5);
        var cursorQuery = cursor is null
            ? string.Empty
            : $"&cursor={Uri.EscapeDataString(cursor)}";
        var request = Http.CreateRequest("GET",
                $"/api/search?q={Uri.EscapeDataString(term)}" +
                cursorQuery +
                $"&partial={variant.Partial.ToString().ToLowerInvariant()}" +
                $"&searchText={variant.SearchText.ToString().ToLowerInvariant()}" +
                $"&searchUserName={variant.SearchUserName.ToString().ToLowerInvariant()}")
            .WithHeader("Accept", "application/json");
        var response = await Http.Send<CursorPageResponse>(httpClient, request);
        AddContinuation(response, cursorPool, 5);
        return response;
    })
    .WithWarmUpDuration(TimeSpan.FromSeconds(WarmUpSeconds))
    .WithLoadSimulations(Inject(SearchRatePerSecond));

// Keep total write traffic at the one-million-messages-per-day rate while exercising
// both root creation and reply creation paths.
var writeComments = Scenario.Create("write_root_comments", async context =>
        await SendComment(context.InvocationNumber, null))
    .WithWarmUpDuration(TimeSpan.FromSeconds(WarmUpSeconds))
    .WithLoadSimulations(Inject(rootWriteRatePerSecond));

var writeReplies = Scenario.Create("write_replies", async context =>
    {
        var parentId = seed.ParentIds[context.InvocationNumber % seed.ParentIds.Length];
        return await SendComment(context.InvocationNumber, parentId);
    })
    .WithWarmUpDuration(TimeSpan.FromSeconds(WarmUpSeconds))
    .WithLoadSimulations(Inject(ReplyWriteRatePerSecond));

var scenarios = new List<ScenarioProps>
{
    readComments,
    readReplies,
    searchComments,
    writeComments,
    writeReplies
};

if (seed.AncestorIds.Length > 0)
{
    var readAncestors = Scenario.Create("read_search_ancestors", async _ =>
        {
            var query = string.Join("&ids=", seed.AncestorIds.Select(id => Uri.EscapeDataString(id.ToString())));
            return await Http.Send(httpClient,
                Http.CreateRequest("GET", $"/api/comments/ancestors?ids={query}"));
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(WarmUpSeconds))
        .WithLoadSimulations(Inject(AncestorReadRatePerSecond));
    scenarios.Add(readAncestors);
}
else
{
    Console.WriteLine("No indexed ancestor path was found; skipping read_search_ancestors.");
}

if (seed.AttachmentIds.Length > 0)
{
    var readAttachments = Scenario.Create("read_attachments", async context =>
        {
            var attachmentId = seed.AttachmentIds[context.InvocationNumber % seed.AttachmentIds.Length];
            return await Http.Send(httpClient,
                Http.CreateRequest("GET", $"/api/attachments/{attachmentId}"));
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(WarmUpSeconds))
        .WithLoadSimulations(Inject(AttachmentReadRatePerSecond));
    scenarios.Add(readAttachments);
}
else
{
    Console.WriteLine("No stored attachment was found; skipping read_attachments.");
}

NBomberRunner
    .RegisterScenarios(scenarios.ToArray())
    .WithTestSuite("Comments API")
    .WithTestName("one-million-messages-per-day")
    .WithReportFolder(reportFolder)
    .WithReportFileName("comments-load-test")
    .Run();

async Task<IResponse> SendComment(long invocationNumber, Guid? parentId)
{
    var captcha = await Http.Send<CaptchaResponse>(
        httpClient,
        Http.CreateRequest("GET", "/api/captcha"));
    if (captcha.IsError) return captcha;

    var userNumber = invocationNumber % TargetUsersPerDay + 1;
    var userName = $"LoadUser{userNumber}";
    var text = parentId is null
        ? $"Load test comment {invocationNumber}."
        : $"Load test reply {invocationNumber}.";
    using var form = new MultipartFormDataContent
    {
        { new StringContent(userName), "userName" },
        { new StringContent($"{userName}@loadtest.example"), "email" },
        { new StringContent(text), "text" },
        { new StringContent("load-test"), "captchaId" },
        { new StringContent("bypass"), "captchaAnswer" }
    };

    if (parentId is Guid parent)
        form.Add(new StringContent(parent.ToString()), "parentId");

    // Attachments represent one tenth of submitted messages.
    if (invocationNumber % AttachmentEveryNMessages == 0)
    {
        var testAttachment = testAttachments[invocationNumber % testAttachments.Length];
        var attachment = new ByteArrayContent(testAttachment.Content);
        attachment.Headers.ContentType = new MediaTypeHeaderValue(testAttachment.ContentType);
        form.Add(attachment, "attachments", testAttachment.FileName);
    }

    return await Http.Send(httpClient, Http.CreateRequest("POST", "/api/comments").WithBody(form));
}

static LoadSimulation Inject(int rate)
{
    return Simulation.Inject(rate, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(TestDurationMinutes));
}

static T? TakeRandom<T>(ConcurrentBag<T> values) where T : class
{
    var snapshot = values.ToArray();
    return snapshot.Length == 0 ? null : snapshot[Random.Shared.Next(snapshot.Length)];
}

static T? TakeRandomLimited<T>(ConcurrentBag<T> values, int maximum) where T : class
{
    var snapshot = values.Take(maximum).ToArray();
    return snapshot.Length == 0 ? null : snapshot[Random.Shared.Next(snapshot.Length)];
}

static void AddContinuation(
    Response<HttpResponse<CursorPageResponse>> response,
    ConcurrentBag<string> target,
    int maximum)
{
    if (!response.IsError && response.Payload.IsSome())
    {
        var nextCursor = response.Payload.Value.Data.NextCursor;
        if (!string.IsNullOrWhiteSpace(nextCursor) && target.Count < maximum)
            target.Add(nextCursor);
    }
}

static void AddRootContinuation(
    Response<HttpResponse<CursorPageResponse>> response,
    ConcurrentBag<RootContinuation> target,
    string sort,
    bool descending)
{
    if (!response.IsError && response.Payload.IsSome())
    {
        var nextCursor = response.Payload.Value.Data.NextCursor;
        if (!string.IsNullOrWhiteSpace(nextCursor))
            target.Add(new RootContinuation(nextCursor, sort, descending));
    }
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

static async Task<LoadTestSeed> LoadSeedData(HttpClient client, IReadOnlyList<string> searchTerms)
{
    // Seed IDs from the first bounded response. The API no longer exposes page numbers.
    using var response = await client.GetAsync("/api/comments?sort=createdAt&descending=true");
    response.EnsureSuccessStatusCode();
    await using var stream = await response.Content.ReadAsStreamAsync();
    using var document = await JsonDocument.ParseAsync(stream);
    var root = document.RootElement;
    var parentIds = new HashSet<Guid>();
    var attachmentIds = new HashSet<Guid>();

    foreach (var item in root.GetProperty("items").EnumerateArray())
        CollectCommentData(item, parentIds, attachmentIds);

    if (parentIds.Count == 0)
        throw new InvalidOperationException("The API has no comments to use for load testing.");

    var ancestorIds = await FindAncestorIds(client, searchTerms);
    return new LoadTestSeed(
        parentIds.ToArray(),
        attachmentIds.ToArray(),
        ancestorIds);
}

static async Task<Guid[]> FindAncestorIds(HttpClient client, IReadOnlyList<string> searchTerms)
{
    foreach (var term in searchTerms)
    {
        using var response = await client.GetAsync(
            $"/api/search?q={Uri.EscapeDataString(term)}&partial=false&searchText=true&searchUserName=true");
        if (!response.IsSuccessStatusCode) continue;

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        foreach (var item in document.RootElement.GetProperty("items").EnumerateArray())
        {
            if (!item.TryGetProperty("ancestorIds", out var ancestors)) continue;
            var ids = ancestors.EnumerateArray().Select(x => x.GetGuid()).ToArray();
            if (ids.Length > 0) return ids;
        }
    }

    return [];
}

static void CollectCommentData(
    JsonElement comment,
    ISet<Guid> commentIds,
    ISet<Guid> attachmentIds)
{
    // Root responses may already contain small reply trees, so collect IDs recursively
    // for later reply and attachment requests.
    commentIds.Add(comment.GetProperty("id").GetGuid());
    foreach (var attachment in comment.GetProperty("attachments").EnumerateArray())
        attachmentIds.Add(attachment.GetProperty("id").GetGuid());

    foreach (var reply in comment.GetProperty("replies").EnumerateArray())
        CollectCommentData(reply, commentIds, attachmentIds);
}

internal readonly record struct SearchVariant(bool Partial, bool SearchText, bool SearchUserName);

internal sealed record RootContinuation(string Cursor, string Sort, bool Descending);

internal sealed record CursorPageResponse(string? NextCursor);

internal sealed record CaptchaResponse(string Id, string Image);

internal sealed record LoadTestSeed(
    Guid[] ParentIds,
    Guid[] AttachmentIds,
    Guid[] AncestorIds);
