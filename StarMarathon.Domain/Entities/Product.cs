namespace StarMarathon.Domain.Entities;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Price { get; set; }
    public string? ImageUrl { get; set; }

    // Язык контента (ru, en, etc). Или "all".
    public string Language { get; set; } = "ru";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}