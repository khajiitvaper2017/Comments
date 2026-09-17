using Comments.Infrastructure.Options;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Comments.Infrastructure.Search;

public sealed class ElasticService : IElasticService
{
    private const int PageSize = 25;
    private readonly ElasticsearchClient client;
    private readonly string index;
    private readonly ILogger<ElasticService> logger;

    public ElasticService(IOptions<ElasticsearchOptions> options, ILogger<ElasticService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        var value = options.Value;
        if (!Uri.TryCreate(value.Uri, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("Elasticsearch:Uri must be an absolute URI.");
        if (string.IsNullOrWhiteSpace(value.Index))
            throw new InvalidOperationException("Elasticsearch:Index cannot be empty.");

        client = new ElasticsearchClient(new ElasticsearchClientSettings(uri));
        index = value.Index;
        this.logger = logger;
    }

    public async Task IndexAsync(CommentSearchDocument document, CancellationToken ct)
    {
        var response = await client.IndexAsync(document, request => request.Index(index).Id(document.Id), ct);
        if (!response.IsValidResponse)
            throw new InvalidOperationException($"Elasticsearch indexing comment {document.Id} failed.");
    }

    public async Task BulkIndexAsync(IReadOnlyCollection<CommentSearchDocument> documents, CancellationToken ct)
    {
        if (documents.Count == 0) return;
        var response = await client.IndexManyAsync(documents, index, ct);
        if (!response.IsValidResponse || response.Errors)
            throw new InvalidOperationException("Elasticsearch bulk indexing failed.");
    }

    public async Task<IReadOnlyList<CommentSearchDocument>> SearchAsync(
        string query,
        bool partial,
        bool searchText,
        bool searchUserName,
        bool searchComments,
        bool searchReplies,
        string? cursor,
        CancellationToken ct)
    {
        var response = await client.SearchAsync<CommentSearchDocument>(request =>
        {
            request.Indices(index)
                .Size(PageSize)
                .Sort(sort => sort.Field("id.keyword", SortOrder.Asc));
            if (!string.IsNullOrWhiteSpace(cursor))
                request.SearchAfter(FieldValue.String(cursor));

            request.Query(queryDefinition =>
            {
                if (searchComments && searchReplies)
                {
                    ConfigureSearchQuery(queryDefinition, query, partial, searchText, searchUserName);
                    return;
                }

                queryDefinition.Bool(boolQuery =>
                {
                    if (searchComments)
                        boolQuery.MustNot(innerQuery => innerQuery.Exists(exists => exists
                            .Field(document => document.ParentId)));
                    else
                        boolQuery.Filter(innerQuery => innerQuery.Exists(exists => exists
                            .Field(document => document.ParentId)));

                    boolQuery.Must(innerQuery => ConfigureSearchQuery(
                        innerQuery, query, partial, searchText, searchUserName));
                });
            });
        }, ct);

        if (response.IsValidResponse) return response.Documents.ToList();

        logger.LogError(
            "Elasticsearch search failed for cursor {Cursor}, partial={Partial}, searchText={SearchText}, " +
            "searchUserName={SearchUserName}, searchComments={SearchComments}, searchReplies={SearchReplies}, " +
            "status={StatusCode}, error={ProductError}. {DebugInformation}",
            cursor, partial, searchText, searchUserName, searchComments, searchReplies,
            response.ApiCallDetails.HttpStatusCode,
            response.ApiCallDetails.ProductError,
            response.ApiCallDetails.DebugInformation);
        throw new InvalidOperationException("Elasticsearch search failed.");
    }

    public async Task<bool> IndexExistsAsync(CancellationToken ct)
    {
        var response = await client.Indices.ExistsAsync(index, ct);
        return !response.IsValidResponse
            ? throw new InvalidOperationException("Elasticsearch index check failed.")
            : response.Exists;
    }

    public async Task<long> CountAsync(CancellationToken ct)
    {
        var response = await client.SearchAsync<CommentSearchDocument>(request => request
            .Indices(index)
            .Size(0)
            .TrackTotalHits(true), ct);
        return !response.IsValidResponse
            ? throw new InvalidOperationException("Elasticsearch document count failed.")
            : response.Total;
    }

    public async Task DeleteIndexAsync(CancellationToken ct)
    {
        var response = await client.Indices.DeleteAsync(index, ct);
        if (!response.IsValidResponse)
            throw new InvalidOperationException("Elasticsearch index deletion failed.");
    }

    private static void ConfigureTextSearch(
        MultiMatchQueryDescriptor<CommentSearchDocument> descriptor,
        string query,
        bool searchText,
        bool searchUserName,
        bool partial = true)
    {
        descriptor.Query(query).Type(partial ? TextQueryType.PhrasePrefix : TextQueryType.Phrase);
        switch (searchText)
        {
            case true when searchUserName:
                descriptor.Fields(document => document.Text, document => document.UserName);
                break;
            case true:
                descriptor.Fields(document => document.Text);
                break;
            default:
            {
                if (searchUserName)
                    descriptor.Fields(document => document.UserName);
                break;
            }
        }
    }

    private static void ConfigureSearchQuery(
        QueryDescriptor<CommentSearchDocument> descriptor,
        string query,
        bool partial,
        bool searchText,
        bool searchUserName)
    {
        if (partial && searchUserName)
        {
            descriptor.Bool(boolQuery => boolQuery.Should(
                innerQuery => innerQuery.MultiMatch(multiMatch =>
                    ConfigureTextSearch(multiMatch, query, searchText, searchUserName)),
                innerQuery => innerQuery.Wildcard(wildcard => wildcard
                    .Field("userName.keyword")
                    .Value($"*{EscapeWildcard(query)}*")
                    .CaseInsensitive())));
            return;
        }

        descriptor.MultiMatch(multiMatch =>
            ConfigureTextSearch(multiMatch, query, searchText, searchUserName, partial));
    }

    private static string EscapeWildcard(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("?", "\\?", StringComparison.Ordinal);
    }
}
