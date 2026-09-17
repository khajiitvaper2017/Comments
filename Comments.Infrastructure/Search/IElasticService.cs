namespace Comments.Infrastructure.Search;

public interface IElasticService
{
    Task IndexAsync(CommentSearchDocument document, CancellationToken ct);

    Task BulkIndexAsync(IReadOnlyCollection<CommentSearchDocument> documents, CancellationToken ct);

    Task<IReadOnlyList<CommentSearchDocument>> SearchAsync(
        string query,
        bool partial,
        bool searchText,
        bool searchUserName,
        bool searchComments,
        bool searchReplies,
        string? cursor,
        CancellationToken ct);

    Task<bool> IndexExistsAsync(CancellationToken ct);

    Task<long> CountAsync(CancellationToken ct);

    Task DeleteIndexAsync(CancellationToken ct);
}
