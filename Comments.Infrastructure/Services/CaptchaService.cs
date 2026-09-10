using System.Collections.Concurrent;
using System.Text;
using Comments.Application.Abstractions;
using Comments.Application.DTOs;

namespace Comments.Infrastructure.Services;

public sealed class CaptchaService : ICaptchaService
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly ConcurrentDictionary<string, (string Answer, DateTime Expiry)> entries = new();

    public CaptchaDto Create()
    {
        var id = Guid.NewGuid().ToString("N");
        var answer = CreateAnswer();
        entries[id] = (answer, DateTime.UtcNow.AddMinutes(5));
        var svg = CreateSvg(answer);
        return new CaptchaDto(id,
            $"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))}");
    }

    public bool Verify(string id, string answer)
    {
        if (!entries.TryRemove(id, out var item) || item.Expiry < DateTime.UtcNow) return false;
        return item.Answer.Equals(answer.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateAnswer()
    {
        var answer = new StringBuilder(5);
        for (var i = 0; i < 5; i++)
            answer.Append(Alphabet[Random.Shared.Next(Alphabet.Length)]);
        return answer.ToString();
    }

    private static string CreateSvg(string answer)
    {
        var random = Random.Shared;
        var svg = new StringBuilder(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"220\" height=\"72\" viewBox=\"0 0 220 72\">");
        svg.Append("<rect width=\"220\" height=\"72\" rx=\"8\" fill=\"#f8fafc\"/>");

        for (var i = 0; i < 9; i++)
        {
            var y = random.Next(8, 65);
            var bend = random.Next(-18, 19);
            svg.Append(
                $"<path d=\"M-10 {y} Q 55 {y + bend} 115 {y} T 230 {y + random.Next(-12, 13)}\" fill=\"none\" stroke=\"#b8c7dc\" stroke-width=\"{random.Next(1, 3)}\" opacity=\".8\"/>");
        }

        for (var i = 0; i < 30; i++)
            svg.Append(
                $"<circle cx=\"{random.Next(5, 216)}\" cy=\"{random.Next(5, 68)}\" r=\"{random.Next(1, 3)}\" fill=\"#8ea5c2\" opacity=\".55\"/>");

        for (var i = 0; i < answer.Length; i++)
        {
            var x = 26 + i * 41 + random.Next(-3, 4);
            var y = random.Next(47, 57);
            var angle = random.Next(-22, 23);
            var color = i % 2 == 0 ? "#173b70" : "#315f98";
            svg.Append(
                $"<text x=\"{x}\" y=\"{y}\" transform=\"rotate({angle} {x} {y})\" font-family=\"Arial\" font-size=\"34\" font-weight=\"bold\" fill=\"{color}\">{answer[i]}</text>");
        }

        svg.Append("</svg>");
        return svg.ToString();
    }
}
