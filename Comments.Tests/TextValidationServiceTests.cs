using Comments.Infrastructure.Exceptions;
using Comments.Infrastructure.Services;

namespace Comments.Tests;

public sealed class TextValidationServiceTests
{
    private readonly TextValidationService service = new();

    [Theory]
    [InlineData("plain text")]
    [InlineData("<strong>bold</strong> <i>text</i>")]
    [InlineData("<code><strong>nested</strong></code>")]
    [InlineData("<a href=\"https://example.com\" title=\"Example\">link</a>")]
    public void AcceptsAllowedMarkup(string input)
    {
        var result = service.SanitizeAndValidate(input);

        Assert.NotEmpty(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<strong>unclosed")]
    [InlineData("<strong><i>mismatched</strong></i>")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<u>unsupported</u>")]
    [InlineData("<strong")]
    public void RejectsInvalidMarkup(string input)
    {
        Assert.Throws<ValidationException>(() => service.SanitizeAndValidate(input));
    }

    [Fact]
    public void RejectsTextOverFiveThousandCharacters()
    {
        Assert.Throws<ValidationException>(() => service.SanitizeAndValidate(new string('x', 5001)));
    }

    [Fact]
    public void KeepsHttpLinksAndRemovesDisallowedLinkSchemes()
    {
        var result = service.SanitizeAndValidate(
            "<a href=\"https://example.com\">safe</a><a href=\"javascript:alert(1)\">bad</a>");

        Assert.Contains("https://example.com", result);
        Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
    }
}
