using Comments.Application.DTOs;

namespace Comments.Application.Abstractions;

public interface ICaptchaService
{
    CaptchaDto Create();
    bool Verify(string id, string answer);
}
