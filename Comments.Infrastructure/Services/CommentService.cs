using System.Text.Json;
using System.Text.RegularExpressions;
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
            "userName" => descending ? query.OrderByDescending(x => x.UserName) : query.OrderBy(x => x.UserName),
            "email" => descending ? query.OrderByDescending(x => x.Email) : query.OrderBy(x => x.Email),
            _ => descending ? query.OrderByDescending(x => x.CreatedAtUtc) : query.OrderBy(x => x.CreatedAtUtc)
        };
        var total = await query.CountAsync(ct);
        var roots = await query.Skip((page - 1) * 25).Take(25).Include(x => x.Attachments).ToListAsync(ct);
        var rootIds = roots.Select(x => x.Id).ToArray();
        var replies = rootIds.Length == 0
            ? []
            : await db.Comments.AsNoTracking()
                .Where(x => !x.IsDeleted && x.ParentId != null && rootIds.Contains(x.RootId))
                .Include(x => x.Attachments)
                .OrderBy(x => x.CreatedAtUtc)
                .ToListAsync(ct);
        var commentsForPage = roots.Concat(replies).ToList();
        var result = new CommentPageDto(
            roots.Select(x => Map(x, commentsForPage)).ToList(),
            page,
            25,
            total,
            sort,
            descending);
        await cache.SetAsync(page, sort, descending, result, ct);
        return result;
    }

    public async Task<CommentDto> CreateAsync(CreateCommentRequest request,
        IReadOnlyList<AttachmentInput> files, string? ip, string? agent, CancellationToken ct)
    {
        // Files are stored before the transaction so the database row can reference their paths;
        // image conversion is deferred to the attachment queue.
        Validate(request);
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
            HomePage = NormalizeHomePage(request.HomePage),
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
        if (comment.ParentId is null)
            AddOutbox(new CommentCreated(comment.Id, comment.CreatedAtUtc));
        else
            AddOutbox(new ReplyCreated(comment.Id, comment.ParentId.Value, comment.CreatedAtUtc));
        foreach (var attachment in comment.Attachments.Where(x =>
                     x.ProcessingStatus == AttachmentProcessingStatus.Pending))
            AddOutbox(new ProcessAttachment(attachment.Id));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Map(comment, [comment]);
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

    private static void Validate(CreateCommentRequest request)
    {
        if (!Regex.IsMatch(request.UserName.Trim(), "^[A-Za-z0-9]+$") || request.UserName.Length > 100)
            throw new ValidationException("User Name must contain only Latin letters and digits.");
        if (!Regex.IsMatch(request.Email, "^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}$"))
            throw new ValidationException("A valid e-mail is required.");
        _ = NormalizeHomePage(request.HomePage);
    }

    private static string? NormalizeHomePage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (!normalized.Contains("://", StringComparison.Ordinal)) normalized = "https://" + normalized;
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || string.IsNullOrWhiteSpace(uri.Host))
            throw new ValidationException("Home page must be a valid HTTP(S) URL.");
        return uri.ToString();
    }

    private static CommentDto Map(Comment comment, IEnumerable<Comment> all)
    {
        var replies = all.Where(x => x.ParentId == comment.Id).OrderBy(x => x.CreatedAtUtc)
            .Select(x => Map(x, all)).ToList();
        return new CommentDto(comment.Id, comment.ParentId, comment.UserName, comment.Email, comment.HomePage,
            comment.SanitizedText, comment.CreatedAtUtc,
            comment.Attachments.Select(a => new AttachmentDto(a.Id, a.OriginalName, a.ContentType, a.Size,
                a.Width, a.Height)).ToList(), replies);
    }
}
