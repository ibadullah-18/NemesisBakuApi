using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class UploadController : ControllerBase
{
    private readonly IFileService _fileService;

    public UploadController(IFileService fileService)
    {
        _fileService = fileService;
    }

    [HttpPost("image")]
    public async Task<IActionResult> UploadImage(IFormFile file, [FromQuery] string folder = "general")
    {
        var imageUrl = await _fileService.UploadImageAsync(file, folder);

        return Ok(ApiResponse<string>.Ok(imageUrl, "Şəkil uğurla yükləndi"));
    }
}