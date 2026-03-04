using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StarMarathon.Infrastructure.Persistence;
using System.Security.Claims;

namespace StarMarathon.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly StarDbContext _db;

    public UserController(StarDbContext db)
    {
        _db = db;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var user = await _db.Profiles.FindAsync(userId);

        if (user == null) return NotFound();

        return Ok(new
        {
            user.Id,
            user.Username,
            user.Role,
            user.PhoneNumber,
            user.LanguageCode,
            user.AvatarUrl
        });
    }

    public record SetLanguageRequest(string Language);

    [HttpPost("set-language")]
    public async Task<IActionResult> SetLanguage([FromBody] SetLanguageRequest req)
    {
        var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var user = await _db.Profiles.FindAsync(userId);

        if (user == null) return NotFound();

        var allowed = new[] { "ru", "en", "ge", "am" };
        if (!allowed.Contains(req.Language))
        {
            return BadRequest("Use: ru, en, ge, am");
        }

        user.LanguageCode = req.Language;
        await _db.SaveChangesAsync();

        return Ok(new { success = true, language = user.LanguageCode });
    }
}   