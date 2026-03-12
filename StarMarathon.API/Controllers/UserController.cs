using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StarMarathon.Application.Interfaces;
using StarMarathon.Infrastructure.Persistence;
using System.Security.Claims;
using System.Linq; // Добавлено для работы с LINQ (Where, Select, ToList)

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

    // НОВЫЙ МЕТОД: Лидерборд (с аватарками из БД)
    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard()
    {
        var authJwt = User.FindFirst("ext_token")?.Value;

        if (string.IsNullOrEmpty(authJwt) || authJwt == "admin_auth_jwt")
            return Ok(new List<object>()); // Пустой массив, если нет токена

        try
        {
            // 1. Получаем рейтинг из Python
            var rating = await _storm.GetRatingAsync(authJwt);

            // 2. Собираем все непустые tg_id из ответа Python
            var tgIds = rating
                .Where(r => r.Tg_Id.HasValue)
                .Select(r => r.Tg_Id.Value)
                .ToList();

            // 3. Одним запросом достаем аватарки из нашей базы
            var avatars = await _db.Profiles
                .Where(p => tgIds.Contains(p.Id))
                .Select(p => new { p.Id, p.AvatarUrl })
                .ToDictionaryAsync(p => p.Id, p => p.AvatarUrl);

            // 4. Склеиваем данные для фронтенда
            var enrichedRating = rating.Select(r => new
            {
                fio = r.Fio,
                balance = r.Balance,
                place = r.Place,
                // Ищем аватарку по tg_id. Если нет tg_id или аватарки в БД — будет null
                avatarUrl = r.Tg_Id.HasValue && avatars.ContainsKey(r.Tg_Id.Value)
                            ? avatars[r.Tg_Id.Value]
                            : null
            });

            return Ok(enrichedRating);
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