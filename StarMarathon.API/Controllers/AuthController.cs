using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StarMarathon.Application.Interfaces;
using StarMarathon.Domain.Entities;
using StarMarathon.Infrastructure.Persistence;

namespace StarMarathon.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _storm;
    private readonly StarDbContext _db;
    private readonly IConfiguration _cfg;

    public AuthController(IAuthService storm, StarDbContext db, IConfiguration cfg)
    {
        _storm = storm;
        _db = db;
        _cfg = cfg;
    }

    public record LoginRequest(long TgId, string Pin, string? Username, string? Phone);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        // 1. Идем во внешнее API
        var (sessionJwt, authJwt) = await _storm.LoginAsync(req.TgId, req.Pin, req.Username, req.Phone);

        if (string.IsNullOrEmpty(sessionJwt))
        {
            // Для Админа (тестовый вход, байпас внешнего API)
            // ИЗМЕНЕНИЕ: Теперь проверяем 9 девяток
            if (req.Pin == "999999999")
            {
                sessionJwt = "admin_bypass_token"; // Фейковый токен
            }
            else
            {
                return Unauthorized(new { error = "Неверный пин-код или ошибка сервера" });
            }
        }

        // 2. Работаем с локальной БД (Supabase)
        var user = await _db.Profiles.FindAsync(req.TgId);
        if (user == null)
        {
            user = new UserProfile
            {
                Id = req.TgId,
                Username = req.Username ?? "",
                // ИЗМЕНЕНИЕ: При создании, если пин 9 девяток — даем админа
                Role = req.Pin == "999999999" ? "admin" : "user",
                ExternalAuthJwt = authJwt
            };
            _db.Profiles.Add(user);
        }
        else
        {
            // ИЗМЕНЕНИЕ: Если существующий юзер ввел 9 девяток — повышаем до админа
            if (req.Pin == "999999999") user.Role = "admin";

            user.ExternalAuthJwt = authJwt; // Обновляем токен
        }

        await _db.SaveChangesAsync();

        // 3. Выдаем наш токен с зашитым внутри session_jwt
        var token = GenerateJwt(user, sessionJwt);

        return Ok(new { token, role = user.Role, language = user.LanguageCode });
    }

    [HttpGet("check-phone")]
    public async Task<IActionResult> CheckPhone([FromQuery] long tgId)
    {
        var user = await _db.Profiles.FindAsync(tgId);

        if (user != null && !string.IsNullOrEmpty(user.PhoneNumber))
        {
            return Ok(new { hasPhone = true, phone = user.PhoneNumber });
        }

        return Ok(new { hasPhone = false });
    }

    private string GenerateJwt(UserProfile user, string extToken)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["JWT_KEY"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("ext_token", extToken) // Внешний токен внутри нашего
        }; 

        var token = new JwtSecurityToken(
            issuer: "StarMarathon",
            audience: "StarMarathonClient",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}