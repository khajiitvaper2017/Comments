using System.Collections.Concurrent;
using System.Text;
using Comments.Application.Abstractions;
using Comments.Application.DTOs;
using ImageMagick;
using ImageMagick.Drawing;

namespace Comments.Infrastructure.Services;

public sealed class CaptchaService : ICaptchaService
{
    // This is a local placeholder. Production should use a managed token-based provider like Turnstile
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly ConcurrentDictionary<string, (string Answer, DateTime Expiry)> entries = new();

    public CaptchaDto Create()
    {
        var id = Guid.NewGuid().ToString("N");
        var answer = CreateAnswer();
        entries[id] = (answer, DateTime.UtcNow.AddMinutes(5));
        return new CaptchaDto(id,
            $"data:image/png;base64,{Convert.ToBase64String(RenderCaptcha(answer))}");
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

    private static byte[] RenderCaptcha(string answer)
    {
        var fontPath = Path.Combine(AppContext.BaseDirectory, "Fonts", "Arial.ttf");
        if (!File.Exists(fontPath))
            throw new InvalidOperationException($"Could not load CAPTCHA font: {fontPath}");

        using var image = new MagickImage(new MagickColor("#f8fafc"), 220, 72);
        var random = Random.Shared;
        var drawables = new Drawables()
            .StrokeColor(new MagickColor("#b8c7dc"))
            .StrokeWidth(1)
            .FillColor(MagickColors.None);

        for (var i = 0; i < 9; i++)
        {
            var y = random.Next(8, 65);
            drawables.Line(-10, y, 230, y + random.Next(-12, 13));
        }

        drawables.FillColor(new MagickColor("#8ea5c2"));
        for (var i = 0; i < 30; i++)
        {
            var x = random.Next(5, 216);
            var y = random.Next(5, 68);
            var radius = random.Next(1, 3);
            drawables.Circle(x, y, x + radius, y);
        }

        for (var i = 0; i < answer.Length; i++)
        {
            var x = 26 + i * 41 + random.Next(-3, 4);
            var y = random.Next(47, 57);
            var color = i % 2 == 0 ? "#173b70" : "#315f98";
            drawables
                .PushGraphicContext()
                .Translation(x, y)
                .Rotation(random.Next(-22, 23))
                .Font(fontPath)
                .FontPointSize(34)
                .FillColor(new MagickColor(color))
                .Text(0, 0, answer[i].ToString())
                .PopGraphicContext();
        }

        image.Draw(drawables);
        return image.ToByteArray(MagickFormat.Png);
    }
}
