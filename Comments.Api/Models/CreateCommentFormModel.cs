namespace Comments.Api.Models;

public sealed class CreateCommentFormModel
{
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? HomePage { get; set; }
    public string Text { get; set; } = "";
    public string CaptchaId { get; set; } = "";
    public string CaptchaAnswer { get; set; } = "";
    public Guid? ParentId { get; set; }
    public List<IFormFile>? Attachments { get; set; }
}
