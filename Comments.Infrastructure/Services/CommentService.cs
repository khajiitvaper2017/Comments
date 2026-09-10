using System.Text.RegularExpressions;
using Comments.Application.Abstractions;
using Comments.Application.Data;
using Comments.Application.DTOs;
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
    IAttachmentStorageService attachments) : ICommentService
{
    public async Task<CommentPageDto> GetRootsAsync(int page, string sort, bool descending, CancellationToken ct)
    {
        page = Math.Max(1, page);
        sort = new[] { "userName", "email", "createdAt" }.Contains(sort) ? sort : "createdAt";
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
        return new CommentPageDto(
            roots.Select(x => Map(x, commentsForPage)).ToList(),
            page,
            25,
            total,
            sort,
            descending);
    }

    public async Task<CommentDto> CreateAsync(CreateCommentRequest request,
        IReadOnlyList<AttachmentInput> files, string? ip, string? agent, CancellationToken ct)
    {
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
        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);
        return Map(comment, [comment]);
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
