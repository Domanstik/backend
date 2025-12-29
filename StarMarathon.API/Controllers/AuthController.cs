using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
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
        // 1. Отправляем РЕАЛЬНЫЕ данные во внешнее API
        // (включая телефон, который пришел с фронта)
        var (sessionJwt, authJwt) = await _storm.LoginAsync(req.TgId, req.Pin, req.Username, req.Phone);

        // Если внешнее API не вернуло токен — значит данные не приняты
        if (string.IsNullOrEmpty(sessionJwt))
        {
            // Оставляем бэкдор ТОЛЬКО для админа (на всякий случай, чтобы ты мог войти в админку)
            if (req.Pin == "999999999")
            {
                sessionJwt = "admin_bypass_token";
            }
            else
            {
                // Возвращаем 401, если API отклонило
                return Unauthorized(new { error = "Внешний сервис отклонил вход. Проверьте консоль сервера." });
            }
        }

        // 2. Логика Supabase (синхронизация)
        var user = await _db.Profiles.FindAsync(req.TgId);

        if (user == null)
        {
            user = new UserProfile
            {
                Id = req.TgId,
                Username = req.Username ?? "",
                // Если пин 999... - админ, иначе юзер
                Role = req.Pin == "999999999" ? "admin" : "user",
                PhoneNumber = req.Phone,
                ExternalAuthJwt = authJwt
            };
            _db.Profiles.Add(user);
        }
        else
        {
            if (req.Pin == "999999999") user.Role = "admin";
            // Обновляем телефон, если он пришел новый
            if (!string.IsNullOrEmpty(req.Phone)) user.PhoneNumber = req.Phone;
            if (!string.IsNullOrEmpty(authJwt)) user.ExternalAuthJwt = authJwt;
        }

        await _db.SaveChangesAsync();

        // 3. Выдача нашего токена
        var token = GenerateJwt(user, sessionJwt);

        return Ok(new { token, role = user.Role, language = user.LanguageCode });
    }

    // ... CheckPhone и GenerateJwt оставляем без изменений ...
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