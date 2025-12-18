using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StarMarathon.Application.Interfaces;
using StarMarathon.Infrastructure.Persistence;
using System.Security.Claims;

namespace StarMarathon.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IAuthService _storm;
    private readonly StarDbContext _db;

    public UserController(IAuthService storm, StarDbContext db)
    {
        _storm = storm;
        _db = db;
    }

    private string GetExtToken() => User.FindFirst("ext_token")?.Value ?? "";
    private long GetTgId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var extToken = GetExtToken();

        // Запрос баланса во внешку
        var stormProfile = await _storm.GetProfileAsync(extToken);

        // Данные из нашей базы
        var user = await _db.Profiles.FindAsync(GetTgId());

        return Ok(new
        {
            user?.Id,
            user?.Username,
            user?.AvatarUrl,
            user?.Role,
            Balance = stormProfile.Balance, // Баланс из внешки
            Fio = stormProfile.Fio // ФИО из внешки
        });
    }

    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard()
    {
        var list = await _storm.GetRatingAsync(GetExtToken());
        return Ok(list);
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications()
    {
        // Транзакции из внешки
        var transactions = await _storm.GetTransactionsAsync(GetExtToken());

        // Превращаем в формат уведомлений для фронта
        var notifs = transactions.Select(t => new
        {
            Id = Guid.NewGuid(),
            Title = t.Amount > 0 ? "Начисление звезд" : "Списание звезд",
            Description = $"{t.Descr} ({t.Amount})",
            Date = t.Date,
            IsRead = true
        });

        return Ok(notifs);
    }
}