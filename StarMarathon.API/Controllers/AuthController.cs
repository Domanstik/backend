using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StarMarathon.Application.Interfaces;
using StarMarathon.Domain.Entities;
using StarMarathon.Infrastructure.Persistence;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

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

    // ИСПРАВЛЕНИЕ: Добавили AvatarUrl в принимаемые параметры
    public record LoginRequest(long TgId, string Pin, string? Username, string? Phone, string? LanguageCode, string? AvatarUrl);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        string? sessionJwt = "";
        string? authJwt = "";

        // 1. Проверка админа
        if (req.Pin == "999999999")
        {
            sessionJwt = "admin_bypass_token";
            authJwt = "admin_auth_jwt";
        }
        else
        {
            try
            {
                (sessionJwt, authJwt) = await _storm.LoginAsync(req.TgId, req.Pin, req.Username, req.Phone);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Auth] External API Error: {ex.Message}");
            }
        }

        if (string.IsNullOrEmpty(sessionJwt) || string.IsNullOrEmpty(authJwt))
        {
            return Unauthorized(new { error = "Неверный код или внешний сервис недоступен" });
        }

        // 2. Логика языка
        string incomingLang = req.LanguageCode?.ToLower() ?? "ru";
        string finalLang = "ru";

        if (incomingLang == "en" || incomingLang == "gb") finalLang = "en";
        else if (incomingLang == "ge") finalLang = "ge";
        else if (incomingLang == "am") finalLang = "am";
        else if (incomingLang == "ru") finalLang = "ru";

        // 3. Работа с БД
        var user = await _db.Profiles.FindAsync(req.TgId);

        if (user == null)
        {
            user = new UserProfile
            {
                Id = req.TgId,
                Username = req.Username ?? "",
                Role = req.Pin == "999999999" ? "admin" : "user",
                PhoneNumber = req.Phone,
                ExternalAuthJwt = authJwt,
                LanguageCode = finalLang,
                AvatarUrl = req.AvatarUrl, // <-- ВАЖНО: Сохраняем фото при первой регистрации
                CreatedAt = DateTime.UtcNow
            };
            _db.Profiles.Add(user);
        }
        else
        {
            if (req.Pin == "999999999") user.Role = "admin";
            if (!string.IsNullOrEmpty(req.Phone)) user.PhoneNumber = req.Phone;
            if (!string.IsNullOrEmpty(authJwt)) user.ExternalAuthJwt = authJwt;
            if (string.IsNullOrEmpty(user.LanguageCode)) user.LanguageCode = "ru";

            // <-- ВАЖНО: Обновляем фото, если юзер зашел заново и картинка обновилась
            if (!string.IsNullOrEmpty(req.AvatarUrl)) user.AvatarUrl = req.AvatarUrl;
        }

        await _db.SaveChangesAsync();

        var token = GenerateJwt(user, authJwt);

        return Ok(new { token, role = user.Role, language = user.LanguageCode });
    }

    [HttpGet("check-phone")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckPhone([FromQuery] long tgId)
    {
        var user = await _db.Profiles.AsNoTracking().FirstOrDefaultAsync(u => u.Id == tgId);
        if (user == null || string.IsNullOrEmpty(user.PhoneNumber))
            return Ok(new { hasPhone = false });

        return Ok(new { hasPhone = true, phone = user.PhoneNumber });
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
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}