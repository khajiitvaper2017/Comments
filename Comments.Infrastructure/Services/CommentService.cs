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
    // AutoLoadReplyLimit controls branch size; this separately caps nesting if stored counters are stale or invalid.
    private const int MaxReplyDepth = 24;

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
        var parent = await db.Comments.AsNoTracking()
            .Where(x => x.Id == parentId && !x.IsDeleted)
            .Select(x => new { x.DescendantCount })
            .SingleOrDefaultAsync(ct);
        if (parent is null) return [];

        var replies = await db.Comments.AsNoTracking()
            .Where(x => x.ParentId == parentId && !x.IsDeleted)
            .Include(x => x.Attachments)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(ReplyPageSize)
            .ToListAsync(ct);

        var smallReplyIds = replies
            .Where(x => x.DescendantCount > 0 && x.DescendantCount < AutoLoadReplyLimit)
            .Select(x => x.Id)
            .ToArray();
        var descendants = await LoadDescendantsAsync(smallReplyIds, ct);
        var all = replies.Concat(descendants).ToList();

        return replies
            .Select(x => Map(x, all, 0))
            .ToList();
    }

    /// <summary>Loads one page of root comments together with their replies.</summary>
    public async Task<CommentPageDto> GetRootsAsync(int page, string sort, bool descending, CancellationToken ct)
    {
        // Cache the complete page because the frontend needs the roots and replies together.
        page = Math.Max(1, page);
        sort = new[] { "userName", "email", "createdAt" }.Contains(sort) ? sort : "createdAt";
        var cached = await cache.GetAsync(page, sort, descending, ct);
        if (cached is not null) return cached;
        var query = db.Comments.AsNoTracking().Where(x => x.ParentId == null && !x.IsDeleted);
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
        var total = await query.CountAsync(ct);
        var totalReplyCount = await db.CommentStatistics
            .Where(x => x.Id == 1)
            .Select(x => x.TotalReplyCount)
            .SingleAsync(ct);
        var roots = await query.Skip((page - 1) * 25).Take(25).Include(x => x.Attachments).ToListAsync(ct);
        var smallRootIds = roots
            .Where(x => x.DescendantCount > 0 && x.DescendantCount < AutoLoadReplyLimit)
            .Select(x => x.Id)
            .ToArray();
        var descendants = await LoadDescendantsAsync(smallRootIds, ct);
        var all = roots.Concat(descendants).ToList();
        var result = new CommentPageDto(
            roots.Select(x => Map(x, all, 0)).ToList(),
            page,
            25,
            total,
            sort,
            descending,
            totalReplyCount);
        await cache.SetAsync(page, sort, descending, result, ct);
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
            RawText = request.Text,
            SanitizedText = text,
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
            for (var depth = 0; depth < MaxReplyDepth && ancestorId != Guid.Empty; depth++)
            {
                await db.Comments
                    .Where(x => x.Id == ancestorId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.DescendantCount, x => x.DescendantCount + 1), ct);

                ancestorId = await db.Comments.AsNoTracking()
                    .Where(x => x.Id == ancestorId)
                    .Select(x => x.ParentId ?? Guid.Empty)
                    .SingleAsync(ct);
            }

            await db.CommentStatistics
                .Where(x => x.Id == 1)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.TotalReplyCount, x => x.TotalReplyCount + 1), ct);
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
        return ToDto(comment, [], comment.DescendantCount);
    }

    private async Task<List<Comment>> LoadDescendantsAsync(Guid[] parentIds, CancellationToken ct)
    {
        var descendants = new List<Comment>();
        var frontier = parentIds;

        for (var depth = 0; depth < MaxReplyDepth && frontier.Length > 0; depth++)
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
        var replyCount = comment.DescendantCount;
        if (depth >= MaxReplyDepth || replyCount >= AutoLoadReplyLimit)
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
            comment.SanitizedText, comment.CreatedAtUtc,
            comment.Attachments.Select(a => new AttachmentDto(a.Id, a.OriginalName, a.ContentType, a.Size,
                a.Width, a.Height)).ToList(), replies, replyCount);
    }
}
