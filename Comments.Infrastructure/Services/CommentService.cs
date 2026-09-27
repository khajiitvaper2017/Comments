using System.Globalization;
using System.Text;
using System.Text.Json;
using Comments.Application.Abstractions;
using Comments.Application.DTOs;
using Comments.Application.Events;
using Comments.Application.Jobs;
using Comments.Application.Requests;
using Comments.Domain.Entities;
using Comments.Infrastructure.Exceptions;
using Comments.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comments.Infrastructure.Services;

public sealed class CommentService(
    CommentsDbContext db,
    ITextValidationService validationService,
    ICaptchaService captcha,
    IAttachmentStorageService attachments,
    ICommentCache cache) : ICommentService
{
    private const int RootPageSize = 25;

    /// <summary>
    ///     The maximum number of replies to load automatically for a comment before requiring the user to click "Load more
    ///     replies".
    /// </summary>
    private const int AutoLoadReplyLimit = 5;

    /// <summary>
    ///     The number of replies to load in a single request.
    /// </summary>
    private const int ReplyPageSize = 24;

    /// <summary>Returns the next bounded section of replies for a comment.</summary>
    public async Task<IReadOnlyList<CommentDto>> GetRepliesAsync(Guid parentId, CancellationToken ct)
    {
        var replies = await db.Comments.AsNoTracking()
            .Where(x => x.ParentId == parentId)
            .Include(x => x.Attachments)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(ReplyPageSize)
            .ToListAsync(ct);

        var smallReplyIds = replies
            .Where(x => x.ReplyCount is > 0 and < AutoLoadReplyLimit)
            .Select(x => x.Id)
            .ToArray();
        var descendants = smallReplyIds.Length == 0
            ? []
            : await LoadDescendantsAsync(smallReplyIds, ct);
        var all = replies.Concat(descendants).ToList();

        // Group by parent once so recursive mapping only visits a comment's replies.
        var childrenByParent = all.Where(x => x.ParentId.HasValue).ToLookup(x => x.ParentId!.Value);

        return replies
            .Select(x => Map(x, childrenByParent))
            .ToList();
    }

    /// <summary>Loads a bounded set of comments in the requested order with one database query.</summary>
    public async Task<IReadOnlyList<CommentDto>> GetAncestorsAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var requestedIds = ids.Distinct().ToArray();
        if (requestedIds.Length == 0) return [];

        var comments = await db.Comments.AsNoTracking()
            .Where(x => Enumerable.Contains(requestedIds, x.Id))
            .Include(x => x.Attachments)
            .ToListAsync(ct);
        var byId = comments.ToDictionary(x => x.Id);
        return requestedIds.Where(byId.ContainsKey)
            .Select(id => ToDto(byId[id], [], byId[id].ReplyCount))
            .ToList();
    }

    /// <summary>Loads one bounded root slice together with its permitted replies.</summary>
    public async Task<CommentPageDto> GetRootsAsync(string sort, bool descending,
        string? cursor = null, CancellationToken ct = default)
    {
        // Cache the complete bounded section because the frontend needs roots and replies together.
        sort = new[] { "userName", "email", "createdAt" }.Contains(sort) ? sort : "createdAt";
        var cached = await cache.GetAsync(sort, descending, cursor, ct);
        if (cached is not null) return cached;
        var query = db.Comments.AsNoTracking().Where(x => x.ParentId == null);
        var position = DecodeCursor(cursor);
        if (position is not null && position.Sort == sort && position.Descending == descending)
            query = ApplyCursor(query, sort, descending, position);

        query = sort switch
        {
            "userName" => descending
                ? query.OrderByDescending(x => x.UserName).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.UserName).ThenBy(x => x.Id),
            "email" => descending
                ? query.OrderByDescending(x => x.Email).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.Email).ThenBy(x => x.Id),
            _ => descending
                ? query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
        };
        var roots = await query.Take(RootPageSize + 1).Include(x => x.Attachments).ToListAsync(ct);
        var hasMore = roots.Count > RootPageSize;
        if (hasMore) roots.RemoveAt(RootPageSize);
        var smallRootIds = roots
            .Where(x => x.ReplyCount is > 0 and < AutoLoadReplyLimit)
            .Select(x => x.Id)
            .ToArray();
        // A small root thread is bounded by ReplyCount, so its complete tree can be read
        // by RootId in one query instead of issuing one query per reply depth.
        var descendants = await LoadSmallRootDescendantsAsync(smallRootIds, ct);
        var all = roots.Concat(descendants).ToList();
        // Group by parent once so recursive mapping only visits a comment's replies.
        var childrenByParent = all.Where(x => x.ParentId.HasValue).ToLookup(x => x.ParentId!.Value);
        var result = new CommentPageDto(
            roots.Select(x => Map(x, childrenByParent)).ToList(),
            hasMore ? EncodeCursor(roots[^1], sort, descending) : null,
            sort,
            descending);
        await cache.SetAsync(sort, descending, result, cursor, ct);
        return result;
    }

    public async Task<CommentDto> CreateAsync(CreateCommentRequest request, CancellationToken ct)
    {
        // Files are stored before the transaction so the database row can reference their paths;
        // image conversion is deferred to the attachment queue.
        if (!captcha.Verify(request.CaptchaId, request.CaptchaAnswer))
            throw new ValidationException("CAPTCHA is invalid or expired.");
        var text = validationService.SanitizeAndValidate(request.Text);
        var parent = request.ParentId is null
            ? null
            : await db.Comments.FindAsync([request.ParentId.Value], ct) ??
              throw new ValidationException("Parent comment was not found.");
        var comment = new Comment
        {
            ParentId = request.ParentId,
            RootId = parent?.RootId ?? Guid.Empty,
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim(),
            HomePage = string.IsNullOrWhiteSpace(request.HomePage) ? null : request.HomePage.Trim(),
            Text = text,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent
        };
        if (parent is null) comment.RootId = comment.Id;
        foreach (var file in request.Attachments) comment.Attachments.Add(await attachments.SaveAsync(file, ct));

        // The comment and its outbox messages must commit or roll back together.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Comments.Add(comment);
        if (parent is not null)
        {
            // A reply contributes to every ancestor's total reply count.
            var ancestorId = parent.Id;
            var visited = new HashSet<Guid>();
            // Guard against a malformed parent cycle so this walk cannot run forever.
            while (ancestorId != Guid.Empty && visited.Add(ancestorId))
            {
                var currentAncestorId = ancestorId;
                await db.Comments
                    .Where(x => x.Id == currentAncestorId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.ReplyCount, x => x.ReplyCount + 1), ct);

                ancestorId = await db.Comments
                    .Where(x => x.Id == currentAncestorId)
                    .Select(x => x.ParentId ?? Guid.Empty)
                    .SingleAsync(ct);
            }
        }

        if (comment.ParentId is null)
            AddOutbox(new CommentCreated(comment.Id, comment.CreatedAtUtc));
        else
            AddOutbox(new ReplyCreated(comment.Id, comment.ParentId.Value, comment.CreatedAtUtc));
        foreach (var attachment in comment.Attachments.Where(x =>
                     x.ProcessingStatus == AttachmentProcessingStatus.Pending))
            // WebP conversion is queued.
            AddOutbox(new ProcessAttachment(attachment.Id));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await cache.InvalidateAsync(ct);
        return ToDto(comment, [], comment.ReplyCount);
    }

    private async Task<List<Comment>> LoadDescendantsAsync(Guid[] parentIds, CancellationToken ct)
    {
        var descendants = new List<Comment>();
        var frontier = parentIds;

        while (frontier.Length > 0)
        {
            var currentFrontier = frontier;
            var children = await db.Comments.AsNoTracking()
                .Where(x => x.ParentId.HasValue && Enumerable.Contains(currentFrontier, x.ParentId.Value))
                .Include(x => x.Attachments)
                .OrderBy(x => x.CreatedAtUtc)
                .ToListAsync(ct);

            descendants.AddRange(children);
            frontier = [.. children.Select(x => x.Id)];
        }

        return descendants;
    }

    private static IQueryable<Comment> ApplyCursor(
        IQueryable<Comment> query,
        string sort,
        bool descending,
        CursorPosition position)
    {
        if (sort == "createdAt")
        {
            var value = DateTime.Parse(position.Value, null, DateTimeStyles.RoundtripKind);
            return descending
                ? query.Where(x => x.CreatedAtUtc < value || (x.CreatedAtUtc == value && x.Id < position.Id))
                : query.Where(x => x.CreatedAtUtc > value || (x.CreatedAtUtc == value && x.Id > position.Id));
        }

        return sort == "userName"
            ? descending
                ? query.Where(x => x.UserName.CompareTo(position.Value) < 0 ||
                                   (x.UserName == position.Value && x.Id < position.Id))
                : query.Where(x => x.UserName.CompareTo(position.Value) > 0 ||
                                   (x.UserName == position.Value && x.Id > position.Id))
            : descending
                ? query.Where(x => x.Email.CompareTo(position.Value) < 0 ||
                                   (x.Email == position.Value && x.Id < position.Id))
                : query.Where(x => x.Email.CompareTo(position.Value) > 0 ||
                                   (x.Email == position.Value && x.Id > position.Id));
    }

    private static string EncodeCursor(Comment comment, string sort, bool descending)
    {
        var value = sort switch
        {
            "createdAt" => comment.CreatedAtUtc.ToString("O"),
            "userName" => comment.UserName,
            _ => comment.Email
        };
        var json = JsonSerializer.Serialize(new CursorPosition(sort, descending, value, comment.Id));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static CursorPosition? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            return JsonSerializer.Deserialize<CursorPosition>(json);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<List<Comment>> LoadSmallRootDescendantsAsync(Guid[] rootIds, CancellationToken ct)
    {
        if (rootIds.Length == 0) return [];

        return await db.Comments.AsNoTracking()
            .Where(x => x.ParentId.HasValue && Enumerable.Contains(rootIds, x.RootId))
            .Include(x => x.Attachments)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);
    }

    private void AddOutbox<T>(T message)
    {
        // The dispatcher publishes this serialized application message after the transaction commits.
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = typeof(T).Name,
            Payload = JsonSerializer.Serialize(message)
        });
    }

    private static CommentDto Map(Comment comment, ILookup<Guid, Comment> childrenByParent)
    {
        var replyCount = comment.ReplyCount;
        if (replyCount >= AutoLoadReplyLimit)
            return ToDto(comment, [], replyCount);

        var replies = childrenByParent[comment.Id]
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => Map(x, childrenByParent))
            .ToList();
        return ToDto(comment, replies, replyCount);
    }


    private static CommentDto ToDto(Comment comment, IReadOnlyList<CommentDto> replies, int replyCount)
    {
        var attachments = comment.Attachments.Select(a => new AttachmentDto(a.Id, a.OriginalName, a.ContentType, a.Size,
            a.Width, a.Height)).ToList();
        return new CommentDto(comment.Id, comment.ParentId, comment.UserName, comment.Email, comment.HomePage,
            comment.Text, comment.CreatedAtUtc, attachments, replies, replyCount);
    }

    private sealed record CursorPosition(string Sort, bool Descending, string Value, Guid Id);
}
