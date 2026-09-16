using System.Globalization;
using System.Text;
using System.Text.Json;
using Comments.Application.Abstractions;
using Comments.Application.Data;
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
            .Where(x => x.ParentId == parentId && !x.IsDeleted)
            .Include(x => x.Attachments)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(ReplyPageSize)
            .ToListAsync(ct);

        var smallReplyIds = replies
            .Where(x => x.ReplyCount > 0 && x.ReplyCount < AutoLoadReplyLimit)
            .Select(x => x.Id)
            .ToArray();
        var descendants = smallReplyIds.Length == 0
            ? []
            : await LoadDescendantsAsync(smallReplyIds, ct);
        var all = replies.Concat(descendants).ToList();

        return replies
            .Select(x => Map(x, all, 0))
            .ToList();
    }

    /// <summary>Loads a bounded set of comments in the requested order with one database query.</summary>
    public async Task<IReadOnlyList<CommentDto>> GetAncestorsAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var requestedIds = ids.Distinct().ToArray();
        if (requestedIds.Length == 0) return [];

        var comments = await db.Comments.AsNoTracking()
            .Where(x => !x.IsDeleted && requestedIds.Contains(x.Id))
            .Include(x => x.Attachments)
            .ToListAsync(ct);
        var byId = comments.ToDictionary(x => x.Id);
        return requestedIds.Where(byId.ContainsKey)
            .Select(id => ToDto(byId[id], [], byId[id].ReplyCount))
            .ToList();
    }

    /// <summary>Loads one bounded root slice together with its permitted replies.</summary>
    public async Task<CommentPageDto> GetRootsAsync(string sort, bool descending,
        CancellationToken ct, string? cursor = null)
    {
        // Cache the complete bounded section because the frontend needs roots and replies together.
        sort = new[] { "userName", "email", "createdAt" }.Contains(sort) ? sort : "createdAt";
        var cached = await cache.GetAsync(sort, descending, ct, cursor);
        if (cached is not null) return cached;
        var query = db.Comments.AsNoTracking().Where(x => x.ParentId == null && !x.IsDeleted);
        var position = DecodeCursor(cursor);
        if (position is not null)
        {
            if (position.Sort != sort || position.Descending != descending)
                position = null;
            else
                query = ApplyCursor(query, sort, descending, position);
        }

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
            .Where(x => x.ReplyCount > 0 && x.ReplyCount < AutoLoadReplyLimit)
            .Select(x => x.Id)
            .ToArray();
        // A small root thread is bounded by ReplyCount, so its complete tree can be read
        // by RootId in one query instead of issuing one query per reply depth.
        var descendants = await LoadSmallRootDescendantsAsync(smallRootIds, ct);
        var all = roots.Concat(descendants).ToList();
        var result = new CommentPageDto(
            roots.Select(x => Map(x, all, 0)).ToList(),
            hasMore ? EncodeCursor(roots[^1], sort, descending) : null,
            sort,
            descending);
        await cache.SetAsync(sort, descending, result, ct, cursor);
        return result;
    }

    public async Task<CommentDto> CreateAsync(CreateCommentRequest request,
        IReadOnlyList<AttachmentInput> files, string? ip, string? agent, CancellationToken ct)
    {
        // Files are stored before the transaction so the database row can reference their paths;
        // image conversion is deferred to the attachment queue.
        CommentRequestValidator.Validate(request);
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
            HomePage = CommentRequestValidator.NormalizeHomePage(request.HomePage),
            Text = text,
            IpAddress = ip,
            UserAgent = agent
        };
        if (parent is null) comment.RootId = comment.Id;
        foreach (var file in files) comment.Attachments.Add(await attachments.SaveAsync(file, ct));

        // The comment and its outbox messages must commit or roll back together.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Comments.Add(comment);
        if (parent is not null)
        {
            var ancestorId = parent.Id;
            var visited = new HashSet<Guid>();
            while (ancestorId != Guid.Empty && visited.Add(ancestorId))
            {
                await db.Comments
                    .Where(x => x.Id == ancestorId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.ReplyCount, x => x.ReplyCount + 1), ct);

                ancestorId = await db.Comments.AsNoTracking()
                    .Where(x => x.Id == ancestorId)
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
            var children = await db.Comments.AsNoTracking()
                .Where(x => x.ParentId.HasValue && frontier.Contains(x.ParentId.Value) && !x.IsDeleted)
                .Include(x => x.Attachments)
                .OrderBy(x => x.CreatedAtUtc)
                .ToListAsync(ct);

            descendants.AddRange(children);
            frontier = children.Select(x => x.Id).ToArray();
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
        var value = sort == "createdAt"
            ? comment.CreatedAtUtc.ToString("O")
            : sort == "userName"
                ? comment.UserName
                : comment.Email;
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
            .Where(x => x.ParentId.HasValue && !x.IsDeleted && rootIds.Contains(x.RootId))
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

    private static CommentDto Map(Comment comment, IReadOnlyList<Comment> all, int depth)
    {
        var replyCount = comment.ReplyCount;
        if (replyCount >= AutoLoadReplyLimit)
            return ToDto(comment, [], replyCount);

        var replies = all.Where(x => x.ParentId == comment.Id)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => Map(x, all, depth + 1))
            .ToList();
        return ToDto(comment, replies, replyCount);
    }


    private static CommentDto ToDto(
        Comment comment,
        IReadOnlyList<CommentDto> replies,
        int replyCount)
    {
        return new CommentDto(comment.Id, comment.ParentId, comment.UserName, comment.Email, comment.HomePage,
            comment.Text, comment.CreatedAtUtc,
            comment.Attachments.Select(a => new AttachmentDto(a.Id, a.OriginalName, a.ContentType, a.Size,
                a.Width, a.Height)).ToList(), replies, replyCount);
    }

    private sealed record CursorPosition(string Sort, bool Descending, string Value, Guid Id);
}
