using Comments.Application.Abstractions;
using Comments.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Comments.Api.Controllers;

[ApiController]
[Route("api/captcha")]
public sealed class CaptchaController(ICaptchaService service) : ControllerBase
{
    [HttpGet]
    public CaptchaDto Get()
    {
        return service.Create();
    }
}
