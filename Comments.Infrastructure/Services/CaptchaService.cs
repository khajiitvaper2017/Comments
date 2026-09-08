using System.Collections.Concurrent;
using System.Text;
using Comments.Application.Abstractions;
using Comments.Application.DTOs;

namespace Comments.Infrastructure.Services;

public sealed class CaptchaService : ICaptchaService
{
    private readonly ConcurrentDictionary<string, (string Answer, DateTime Expiry)> entries = new();

    public CaptchaDto Create()
    {
        var id = Guid.NewGuid().ToString("N");
        var answer = Random.Shared.Next(10000, 99999).ToString();
        entries[id] = (answer, DateTime.UtcNow.AddMinutes(5));
        var svg =
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"180\" height=\"60\"><rect width=\"180\" height=\"60\" fill=\"white\"/><path d=\"M0 15h180M0 45h180\" stroke=\"#dbe4f0\"/><text x=\"90\" y=\"40\" text-anchor=\"middle\" font-family=\"Arial\" font-size=\"28\" font-weight=\"bold\" fill=\"#173b70\" letter-spacing=\"5\">{answer}</text></svg>";
        return new CaptchaDto(id,
            $"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))}");
    }

    public bool Verify(string id, string answer)
    {
        if (!entries.TryRemove(id, out var item) || item.Expiry < DateTime.UtcNow) return false;
        return item.Answer == answer.Trim();
    }
}
