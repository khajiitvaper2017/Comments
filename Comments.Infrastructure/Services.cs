using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Text;
using Comments.Application;
using Comments.Domain;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Comments.Infrastructure;

public sealed class StorageOptions
{
    public string Root { get; set; } = "../Comments.Storage/uploads";
    public int MaxTextBytes { get; set; } = 100 * 1024;
}

public sealed class TextPolicy : ITextPolicy
{
    public string SanitizeAndValidate(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 5000)
            throw new ValidationException("Text is required and must be at most 5000 characters.");
        if (Regex.IsMatch(input, "<\\s*(script|style|iframe|img|object|form)|on\\w+\\s*=|javascript:",
                RegexOptions.IgnoreCase)) throw new ValidationException("Unsupported or unsafe HTML.");
        var allowed = Regex.Replace(input, "</?(a|code|i|strong)(?:\\s+[^>]*)?>", "", RegexOptions.IgnoreCase);
        if (allowed.Contains('<') || allowed.Contains('>'))
            throw new ValidationException("Only a, code, i, and strong tags are allowed.");
        var stack = new Stack<string>();
        foreach (Match m in Regex.Matches(input, "<(/?)(a|code|i|strong)(?:\\s+[^>]*)?/?>", RegexOptions.IgnoreCase))
            if (m.Groups[1].Value == "/")
            {
                if (stack.Count == 0 || stack.Pop() != m.Groups[2].Value.ToLowerInvariant())
                    throw new ValidationException("HTML tags must be correctly closed.");
            }
            else if (!m.Value.EndsWith("/>"))
            {
                stack.Push(m.Groups[2].Value.ToLowerInvariant());
            }

        if (stack.Count > 0) throw new ValidationException("HTML tags must be correctly closed.");
        return input;
    }
}

public sealed class CaptchaService : ICaptchaService
{
    private readonly ConcurrentDictionary<string, (string Answer, DateTime Expiry)> entries = new();

    public CaptchaDto Create()
    {
        var id = Guid.NewGuid().ToString("N");
        var answer = Random.Shared.Next(10000, 99999).ToString();
        entries[id] = (answer, DateTime.UtcNow.AddMinutes(5));
        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"180\" height=\"60\"><rect width=\"180\" height=\"60\" fill=\"white\"/><path d=\"M0 15h180M0 45h180\" stroke=\"#dbe4f0\"/><text x=\"90\" y=\"40\" text-anchor=\"middle\" font-family=\"Arial\" font-size=\"28\" font-weight=\"bold\" fill=\"#173b70\" letter-spacing=\"5\">{answer}</text></svg>";
        return new CaptchaDto(id, $"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))}");
    }

    public bool Verify(string id, string answer)
    {
        if (!entries.TryRemove(id, out var item) || item.Expiry < DateTime.UtcNow) return false;
        return item.Answer == answer.Trim();
    }
}

public sealed class CommentService(
    CommentsDbContext db,
    ITextPolicy policy,
    ICaptchaService captcha,
    IOptions<StorageOptions> options,
    IWebHostEnvironment env) : ICommentService
{
    public async Task<CommentPage> GetRootsAsync(int page, string sort, bool descending, CancellationToken ct)
    {
        page = Math.Max(1, page);
        sort = new[] { "userName", "email", "createdAt" }.Contains(sort) ? sort : "createdAt";
        var q = db.Comments.AsNoTracking().Where(x => x.ParentId == null && !x.IsDeleted);
        q = sort switch
        {
            "userName" => descending ? q.OrderByDescending(x => x.UserName) : q.OrderBy(x => x.UserName),
            "email" => descending ? q.OrderByDescending(x => x.Email) : q.OrderBy(x => x.Email),
            _ => descending ? q.OrderByDescending(x => x.CreatedAtUtc) : q.OrderBy(x => x.CreatedAtUtc)
        };
        var total = await q.CountAsync(ct);
        var roots = await q.Skip((page - 1) * 25).Take(25).Include(x => x.Attachments).ToListAsync(ct);
        var all = await db.Comments.AsNoTracking().Where(x => !x.IsDeleted).Include(x => x.Attachments)
            .OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
        return new CommentPage(roots.Select(x => Map(x, all)).ToList(), page, 25, total, sort, descending);
    }

    public async Task<CommentDto> CreateAsync(CreateCommentRequest r, IReadOnlyList<AttachmentInput> files, string? ip,
        string? agent, CancellationToken ct)
    {
        Validate(r);
        if (!captcha.Verify(r.CaptchaId, r.CaptchaAnswer))
            throw new ValidationException("CAPTCHA is invalid or expired.");
        var text = policy.SanitizeAndValidate(r.Text);
        var parent = r.ParentId is null
            ? null
            : await db.Comments.FindAsync([r.ParentId.Value], ct) ??
              throw new ValidationException("Parent comment was not found.");
        var c = new Comment
        {
            ParentId = r.ParentId, RootId = parent?.RootId ?? Guid.Empty, UserName = r.UserName.Trim(),
            Email = r.Email.Trim(), HomePage = string.IsNullOrWhiteSpace(r.HomePage) ? null : r.HomePage.Trim(),
            RawText = r.Text, SanitizedText = text, IpAddress = ip, UserAgent = agent
        };
        if (parent is null) c.RootId = c.Id;
        foreach (var f in files) c.Attachments.Add(await Save(f, ct));
        db.Comments.Add(c);
        await db.SaveChangesAsync(ct);
        return Map(c, [c]);
    }

    private static void Validate(CreateCommentRequest r)
    {
        if (!Regex.IsMatch(r.UserName.Trim(), "^[A-Za-z0-9]+$") || r.UserName.Length > 100)
            throw new ValidationException("User Name must contain only Latin letters and digits.");
        if (!Regex.IsMatch(r.Email, "^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$"))
            throw new ValidationException("A valid e-mail is required.");
        if (!string.IsNullOrWhiteSpace(r.HomePage) && (!Uri.TryCreate(r.HomePage, UriKind.Absolute, out var u) ||
                                                       u.Scheme is not ("http" or "https")))
            throw new ValidationException("Home page must be a valid HTTP(S) URL.");
    }

    private async Task<Attachment> Save(AttachmentInput f, CancellationToken ct)
    {
        var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
        var image = new[] { ".jpg", ".jpeg", ".gif", ".png" }.Contains(ext);
        var text = ext == ".txt";
        if (!image && !text) throw new ValidationException("Only JPG, GIF, PNG, and TXT files are accepted.");
        if (text && f.Content.Length > options.Value.MaxTextBytes)
            throw new ValidationException("TXT files must be at most 100 KB.");
        var id = Guid.NewGuid();
        var dir = Path.Combine(env.ContentRootPath, options.Value.Root);
        Directory.CreateDirectory(dir);
        var stored = id + (image ? ".png" : ".txt");
        var path = Path.Combine(dir, stored);
        int? w = null, h = null;
        if (image)
        {
            using var source = Image.Load(new MemoryStream(f.Content));
            var ratio = Math.Min(320d / source.Width, 240d / source.Height);
            if (ratio < 1) source.Mutate(x => x.Resize((int)(source.Width * ratio), (int)(source.Height * ratio)));
            w = source.Width;
            h = source.Height;
            await source.SaveAsPngAsync(path, ct);
        }
        else
        {
            await File.WriteAllBytesAsync(path, f.Content, ct);
        }

        return new Attachment
        {
            OriginalName = Path.GetFileName(f.FileName), StoredName = stored,
            ContentType = image ? "image/png" : "text/plain", Size = f.Content.Length, StorageReference = path,
            Width = w, Height = h
        };
    }

    private static CommentDto Map(Comment c, IEnumerable<Comment> all)
    {
        var replies = all.Where(x => x.ParentId == c.Id).OrderBy(x => x.CreatedAtUtc).Select(x => Map(x, all)).ToList();
        return new CommentDto(c.Id, c.ParentId, c.UserName, c.Email, c.HomePage, c.SanitizedText, c.CreatedAtUtc,
            c.Attachments.Select(a => new AttachmentDto(a.Id, a.OriginalName, a.ContentType, a.Size, a.Width, a.Height))
                .ToList(), replies);
    }
}

public sealed class ValidationException(string message) : Exception(message);
