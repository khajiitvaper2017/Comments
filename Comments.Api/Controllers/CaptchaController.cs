using Comments.Application.Abstractions;
using Comments.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Comments.Api.Controllers;

[ApiController]
[Route("api/captcha")]
public sealed class CaptchaController(ICaptchaService service) : ControllerBase
{
    /// <summary>Creates a CAPTCHA challenge for comment submission.</summary>
    [HttpGet]
    public CaptchaDto Get()
    {
        return service.Create();
    }
}
