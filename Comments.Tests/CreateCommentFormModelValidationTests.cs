using System.ComponentModel.DataAnnotations;
using Comments.Api.Models;

namespace Comments.Tests;

public sealed class CreateCommentFormModelValidationTests
{
    [Fact]
    public void AcceptsValidForm()
    {
        Assert.True(IsValid());
    }

    [Theory]
    [InlineData("")]
    [InlineData("name with spaces")]
    [InlineData("name!")]
    [InlineData("юзер")]
    [InlineData(" User123")]
    public void RejectsInvalidUserNames(string userName)
    {
        Assert.False(IsValid(form => form.UserName = userName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("name@")]
    [InlineData("пвп\"")]
    public void RejectsInvalidEmails(string email)
    {
        Assert.False(IsValid(form => form.Email = email));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("not-a-url")]
    [InlineData("not a url")]
    public void RejectsInvalidHomePages(string homePage)
    {
        Assert.False(IsValid(form => form.HomePage = homePage));
    }

    [Fact]
    public void RejectsValuesThatExceedDatabaseLimits()
    {
        Assert.False(IsValid(form => form.UserName = new string('a', 101)));
        Assert.False(IsValid(form => form.Email = new string('a', 250) + "@x.com"));
        Assert.False(IsValid(form => form.HomePage = "https://" + new string('a', 248) + ".com"));
        Assert.False(IsValid(form => form.Text = new string('a', 5001)));
    }

    private static bool IsValid(Action<CreateCommentFormModel>? change = null)
    {
        var form = ValidForm();
        change?.Invoke(form);
        var results = new List<ValidationResult>();
        return Validator.TryValidateObject(form, new ValidationContext(form), results, true);
    }

    private static CreateCommentFormModel ValidForm()
    {
        return new CreateCommentFormModel
        {
            UserName = "User123",
            Email = "user@example.com",
            Text = "A valid comment.",
            HomePage = "https://example.com",
            CaptchaId = "captcha",
            CaptchaAnswer = "answer"
        };
    }
}
