using System.ComponentModel.DataAnnotations;

namespace Comments.Api.Models;

public sealed class CreateCommentFormModel
{
    [Required(ErrorMessage = "User Name is required.")]
    [StringLength(100)]
    [RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "User Name must contain only Latin letters and digits.")]
    public string UserName { get; set; } = "";

    [Required(ErrorMessage = "E-mail is required.")]
    [StringLength(254)]
    [EmailAddress(ErrorMessage = "A valid e-mail is required.")]
    public string Email { get; set; } = "";

    [StringLength(254)] [Url] public string? HomePage { get; set; }

    [Required(ErrorMessage = "Text is required.")]
    [StringLength(5000, ErrorMessage = "Text cannot exceed 5000 characters.")]
    public string Text { get; set; } = "";

    [Required(ErrorMessage = "CAPTCHA is required.")]
    public string CaptchaId { get; set; } = "";

    [Required(ErrorMessage = "CAPTCHA answer is required.")]
    public string CaptchaAnswer { get; set; } = "";

    public Guid? ParentId { get; set; }
    public List<IFormFile>? Attachments { get; set; }
}
