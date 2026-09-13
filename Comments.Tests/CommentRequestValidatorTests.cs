using Comments.Application.Requests;
using Comments.Infrastructure.Exceptions;
using Comments.Infrastructure.Services;

namespace Comments.Tests;

public sealed class CommentRequestValidatorTests
{
    [Fact]
    public void AcceptsValidRequest()
    {
        var request = ValidRequest() with { HomePage = "example.com" };

        CommentRequestValidator.Validate(request);

        Assert.Equal("https://example.com/", CommentRequestValidator.NormalizeHomePage(request.HomePage));
    }

    [Theory]
    [InlineData("")]
    [InlineData("name with spaces")]
    [InlineData("name!")]
    [InlineData("юзер")]
    [InlineData(" User123")]
    public void RejectsInvalidUserName(string userName)
    {
        Assert.Throws<ValidationException>(() =>
            CommentRequestValidator.Validate(ValidRequest() with { UserName = userName }));
    }

    [Fact]
    public void RejectsUserNameLongerThanOneHundredCharacters()
    {
        Assert.Throws<ValidationException>(() =>
            CommentRequestValidator.Validate(ValidRequest() with { UserName = new string('a', 101) }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("name@")]
    [InlineData("name@example")]
    [InlineData(" user@example.com")]
    public void RejectsInvalidEmail(string email)
    {
        Assert.Throws<ValidationException>(() =>
            CommentRequestValidator.Validate(ValidRequest() with { Email = email }));
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    public void RejectsInvalidHomePage(string homePage)
    {
        Assert.Throws<ValidationException>(() =>
            CommentRequestValidator.Validate(ValidRequest() with { HomePage = homePage }));
    }

    private static CreateCommentRequest ValidRequest()
    {
        return new CreateCommentRequest(
            "User123", "user@example.com", null, "A valid comment.", "captcha", "answer", null);
    }
}
