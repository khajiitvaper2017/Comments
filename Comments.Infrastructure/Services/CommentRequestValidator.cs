using System.Text.RegularExpressions;
using Comments.Application.Requests;
using Comments.Infrastructure.Exceptions;

namespace Comments.Infrastructure.Services;

public static class CommentRequestValidator
{
    public static void Validate(CreateCommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) ||
            request.UserName.Length > 100 ||
            !Regex.IsMatch(request.UserName, "^[A-Za-z0-9]+$"))
            throw new ValidationException("User Name must contain only Latin letters and digits.");

        if (!Regex.IsMatch(request.Email, "^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}$"))
            throw new ValidationException("A valid e-mail is required.");

        NormalizeHomePage(request.HomePage);
    }

    public static string? NormalizeHomePage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (!normalized.Contains("://", StringComparison.Ordinal)) normalized = "https://" + normalized;
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || string.IsNullOrWhiteSpace(uri.Host))
            throw new ValidationException("Home page must be a valid HTTP(S) URL.");
        return uri.ToString();
    }
}
