using Comments.Application.Abstractions;
using Comments.Application.DTOs;

namespace Comments.Infrastructure.Services;

public sealed class LoadTestCaptchaService : ICaptchaService
{
    private const string Id = "load-test";
    private const string Answer = "bypass";

    public CaptchaDto Create()
    {
        return new CaptchaDto(Id,
            "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    }

    public bool Verify(string id, string answer)
    {
        return string.Equals(id, Id, StringComparison.Ordinal) &&
               string.Equals(answer.Trim(), Answer, StringComparison.Ordinal);
    }
}
