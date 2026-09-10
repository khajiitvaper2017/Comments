using System.Text.RegularExpressions;
using Comments.Application.Abstractions;
using Comments.Infrastructure.Exceptions;
using Ganss.Xss;

namespace Comments.Infrastructure.Services;

public sealed class TextValidationService : ITextValidationService
{
    // This policy is shared by the client-facing validation and the server sanitizer.
    private const string AllowedTags = "a|code|i|strong";
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    public string SanitizeAndValidate(string input)
    {
        // Check the markup shape before sanitizing so malformed input is rejected,
        // rather than silently rewritten into a different comment.
        if (string.IsNullOrWhiteSpace(input) || input.Length > 5000)
            throw new ValidationException("Text is required and must be at most 5000 characters.");
        if (Regex.IsMatch(input, "<[^>]*$") ||
            Regex.IsMatch(input, "</?\\s*(a|code|i|strong)\\b[^>]*$", RegexOptions.IgnoreCase))
            throw new ValidationException("Invalid XHTML.");

        var allowed = Regex.Replace(input, $"</?({AllowedTags})(?:\\s+[^>]*)?>", "", RegexOptions.IgnoreCase);
        if (allowed.Contains('<') || allowed.Contains('>'))
            throw new ValidationException("Invalid XHTML.");

        var stack = new Stack<string>();
        foreach (Match match in Regex.Matches(input, $"<(/?)({AllowedTags})(?:\\s+[^>]*)?/?>",
                     RegexOptions.IgnoreCase))
            if (match.Groups[1].Value == "/")
            {
                if (stack.Count == 0 || stack.Pop() != match.Groups[2].Value.ToLowerInvariant())
                    throw new ValidationException("Invalid XHTML.");
            }
            else if (!match.Value.EndsWith("/>"))
            {
                stack.Push(match.Groups[2].Value.ToLowerInvariant());
            }

        if (stack.Count > 0) throw new ValidationException("Invalid XHTML.");
        return Sanitizer.Sanitize(input);
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        // Allow only the tags and link attributes supported by the comment editor.
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith(["a", "code", "i", "strong"]);
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith(["href", "title"]);
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.UnionWith(["http", "https"]);
        return sanitizer;
    }
}
