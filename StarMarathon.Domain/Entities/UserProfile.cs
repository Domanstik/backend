namespace StarMarathon.Domain.Entities;

public class UserProfile
{
    public long Id { get; set; } // Telegram ID
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public string? PhoneNumber { get; set; }

    // Храним "вечный" токен от внешки
    public string? ExternalAuthJwt { get; set; }

    public string LanguageCode { get; set; } = "ru";
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}