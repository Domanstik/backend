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
        string? sessionJwt = null;
        string? authJwt = null;

        // 1. Попытка РЕАЛЬНОГО входа через внешнее API
        // StormBvService отправит authRegister(pin, phone, tgId)
        var result = await _storm.LoginAsync(req.TgId, req.Pin, req.Username, req.Phone);
        sessionJwt = result.SessionToken;
        authJwt = result.AuthToken;

        // 2. Если внешнее API не пустило (вернуло null), проверяем Админский бэкдор
        if (string.IsNullOrEmpty(sessionJwt))
        {
            if (req.Pin == "999999999") // Админский мастер-пин
            {
                sessionJwt = "admin_bypass_token";
            }
            else
            {
                // Если и не админ, и внешка не пустила — ошибка
                return Unauthorized(new { error = "Неверный пин-код или ошибка сервера" });
            }
        }

        // 3. Сохранение/Обновление в Supabase
        var user = await _db.Profiles.FindAsync(req.TgId);
        if (user == null)
        {
            user = new UserProfile
            {
                Id = req.TgId,
                Username = req.Username ?? "",
                // Если зашли через бэкдор 999..999 — админ, иначе юзер
                Role = req.Pin == "999999999" ? "admin" : "user",
                PhoneNumber = req.Phone,
                ExternalAuthJwt = authJwt
            };
            _db.Profiles.Add(user);
        }
        else
        {
            // Если ввел админский пин — повышаем права
            if (req.Pin == "999999999") user.Role = "admin";

            // Обновляем телефон и токен, если пришли новые
            if (!string.IsNullOrEmpty(req.Phone)) user.PhoneNumber = req.Phone;
            if (!string.IsNullOrEmpty(authJwt)) user.ExternalAuthJwt = authJwt;
        }

        await _db.SaveChangesAsync();

        // 4. Выдача нашего токена
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
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("ext_token", extToken)
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