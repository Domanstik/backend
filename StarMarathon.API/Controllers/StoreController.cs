using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StarMarathon.Application.Interfaces;
using StarMarathon.Domain.Entities;
using StarMarathon.Infrastructure.Persistence;
using System.Security.Claims;

namespace StarMarathon.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StoreController : ControllerBase
{
    private readonly StarDbContext _db;

    public StoreController(StarDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetProducts()
    {
        var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        // ИСПРАВЛЕНИЕ: Сначала фильтруем по ID, потом выбираем поле
        var user = await _db.Profiles
            .Where(u => u.Id == userId) // <--- Фильтр здесь
            .Select(u => new { u.LanguageCode }) // <--- Проекция здесь
            .FirstOrDefaultAsync();

        if (user == null) return Unauthorized();
        string lang = user.LanguageCode ?? "ru";

        // Фильтруем товары
        var products = await _db.Products
            .Where(p => p.IsActive)
            .Where(p => p.Language == lang || p.Language == "all")
            .OrderBy(p => p.Price)
            .ToListAsync();

        return Ok(products);
    }

    [HttpPost("buy/{productId}")]
    [Authorize]
    public async Task<IActionResult> BuyProduct(Guid productId)
    {
        var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var product = await _db.Products.FindAsync(productId);
        if (product == null || !product.IsActive) return NotFound("Товар не найден");

        // Если у вас есть поле Quantity, раскомментируйте
        // if (product.Quantity <= 0) return BadRequest("Товар закончился");
        // product.Quantity -= 1;

        var purchase = new Purchase
        {
            UserId = userId,
            ProductId = productId,
            // Убрал PurchaseDate, так как его нет в вашей модели (EF сам поставит CreatedAt, если настроено)
            // Или используйте CreatedAt = DateTime.UtcNow, если такое поле есть
            PriceAtPurchase = product.Price,
            Status = "completed"
        };

        _db.Purchases.Add(purchase);
        await _db.SaveChangesAsync();

        return Ok(new { success = true });
    }
}