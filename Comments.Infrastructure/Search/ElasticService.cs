using Comments.Application.Requests;
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
        SearchCommentRequest searchRequest,
        CancellationToken ct)
    {
        var response = await client.SearchAsync<CommentSearchDocument>(request =>
        {
            request.Indices(index)
                .Size(PageSize)
                .Sort(sort => sort.Field(document => document.Id, SortOrder.Asc));

            if (!string.IsNullOrWhiteSpace(searchRequest.Cursor))
                request.SearchAfter(FieldValue.String(searchRequest.Cursor));

            request.Query(query => ConfigureQuery(query, searchRequest));
        }, ct);

        if (response.IsValidResponse) return response.Documents.ToList();

        logger.LogError(
            "Elasticsearch search failed for request {SearchRequest}. " +
            "API call details: {ApiCallDetails}",
            searchRequest,
            response.ApiCallDetails);
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
        var response = await client.CountAsync<CommentSearchDocument>(
            request => request.Indices(index), ct);
        return !response.IsValidResponse
            ? throw new InvalidOperationException("Elasticsearch document count failed.")
            : response.Count;
    }

    public async Task DeleteIndexAsync(CancellationToken ct)
    {
        var response = await client.Indices.DeleteAsync(index, ct);
        if (!response.IsValidResponse)
            throw new InvalidOperationException("Elasticsearch index deletion failed.");
    }

    private static void ConfigureQuery(
        QueryDescriptor<CommentSearchDocument> descriptor,
        SearchCommentRequest search)
    {
        if (search is { SearchComments: true, SearchReplies: true })
        {
            ConfigureTextQuery(descriptor, search);
            return;
        }

        descriptor.Bool(boolQuery =>
        {
            if (search.SearchComments)
                boolQuery.MustNot(query => query.Exists(exists => exists.Field(document => document.ParentId)));
            else
                boolQuery.Filter(query => query.Exists(exists => exists.Field(document => document.ParentId)));

            boolQuery.Must(query => ConfigureTextQuery(query, search));
        });
    }

    private static void ConfigureTextQuery(
        QueryDescriptor<CommentSearchDocument> descriptor,
        SearchCommentRequest search)
    {
        if (search is { Partial: true, SearchUserName: true })
        {
            descriptor.Bool(boolQuery => boolQuery.Should(
                query => query.MultiMatch(multiMatch => ConfigureTextSearch(multiMatch, search)),
                query => query.Wildcard(wildcard => wildcard
                    .Field("userName.keyword")
                    .Value($"*{EscapeWildcard(search.Query)}*")
                    .CaseInsensitive())));
            return;
        }

        descriptor.MultiMatch(multiMatch => ConfigureTextSearch(multiMatch, search));
    }

    private static void ConfigureTextSearch(
        MultiMatchQueryDescriptor<CommentSearchDocument> descriptor,
        SearchCommentRequest search)
    {
        descriptor.Query(search.Query)
            .Type(search.Partial ? TextQueryType.PhrasePrefix : TextQueryType.Phrase);

        switch (search.SearchText)
        {
            case true when search.SearchUserName:

                descriptor.Fields(document => document.Text, document => document.UserName);
                break;
            case true:

                descriptor.Fields(document => document.Text);
                break;
            default:
            {
                if (search.SearchUserName)

                    descriptor.Fields(document => document.UserName);
                break;
            }
        }
    }

    private static string EscapeWildcard(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("?", "\\?", StringComparison.Ordinal);
    }
}
