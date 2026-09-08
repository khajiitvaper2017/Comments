using System.Text.RegularExpressions;
using Comments.Application.Abstractions;
using Comments.Infrastructure.Exceptions;

namespace Comments.Infrastructure.Services;

public sealed class TextValidationService : ITextValidationService
{
    private const string AllowedTags = "a|code|i|strong";

    public string SanitizeAndValidate(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 5000)
            throw new ValidationException("Text is required and must be at most 5000 characters.");
        if (Regex.IsMatch(input, "<\\s*(script|style|iframe|img|object|form)|on\\w+\\s*=|javascript:",
                RegexOptions.IgnoreCase))
            throw new ValidationException("Unsupported or unsafe HTML.");
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
        return input;
    }
}
