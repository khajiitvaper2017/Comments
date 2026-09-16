using Comments.Infrastructure.Services;

namespace Comments.Tests;

public sealed class CaptchaServiceTests
{
    [Fact]
    public void CreateReturnsPngCaptcha()
    {
        var captcha = new CaptchaService();

        var result = captcha.Create();

        Assert.StartsWith("data:image/png;base64,", result.ImageDataUrl);
        Assert.NotEmpty(result.Id);
    }
}
