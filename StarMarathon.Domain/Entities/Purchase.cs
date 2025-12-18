namespace StarMarathon.Domain.Entities;

public class Purchase
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Кто купил
    public long UserId { get; set; }
    public UserProfile User { get; set; } = null!;

    // Что купил
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    // Цена на момент покупки (чтобы история не менялась при смене цены товара)
    public int PriceAtPurchase { get; set; }

    // Статус выдачи (pending - ждет выдачи, completed - выдано)
    public string Status { get; set; } = "pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}