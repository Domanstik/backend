using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StarMarathon.Application.Interfaces;
using StarMarathon.Infrastructure.Persistence;
using System.Security.Claims;

namespace StarMarathon.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly StarDbContext _db;
    private readonly IAuthService _storm; // Инжектим наш сервис

    public UserController(StarDbContext db, IAuthService storm)
    {
        _db = db;
        _storm = storm;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var user = await _db.Profiles.FindAsync(userId);

        if (user == null) return NotFound();

        // Достаем authJwt из нашего JWT токена
        var authJwt = User.FindFirst("ext_token")?.Value;

        int currentBalance = 0;

        if (!string.IsNullOrEmpty(authJwt) && authJwt != "admin_auth_jwt")
        {
            try
            {
                // Идем за профилем в Python
                var externalProfile = await _storm.GetProfileAsync(authJwt);
                currentBalance = externalProfile.Balance;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка получения баланса: {ex.Message}");
            }
        }

        return Ok(new
        {
            user = new
            {
                user.Id,
                user.Username,
                user.Role,
                user.PhoneNumber,
                user.LanguageCode,
                user.AvatarUrl
            },
            balance = currentBalance // Отдаем баланс отдельно для фронта
        });
    }

    // НОВЫЙ МЕТОД: Лидерборд
    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard()
    {
        var authJwt = User.FindFirst("ext_token")?.Value;

        if (string.IsNullOrEmpty(authJwt) || authJwt == "admin_auth_jwt")
            return Ok(new List<object>()); // Пустой массив, если нет токена

        try
        {
            var rating = await _storm.GetRatingAsync(authJwt);
            return Ok(rating);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка получения лидерборда: {ex.Message}");
            return Ok(new List<object>());
        }
    }

    // НОВЫЙ МЕТОД: Транзакции/Уведомления
    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications()
    {
        var authJwt = User.FindFirst("ext_token")?.Value;

        if (string.IsNullOrEmpty(authJwt) || authJwt == "admin_auth_jwt")
            return Ok(new List<object>());

        try
        {
            var transactions = await _storm.GetTransactionsAsync(authJwt);
            return Ok(transactions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка получения транзакций: {ex.Message}");
            return Ok(new List<object>());
        }
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