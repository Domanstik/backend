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
    private readonly IAuthService _storm; // Добавь сервис

    public StoreController(StarDbContext db, IAuthService storm)
    {
        _db = db;
        _storm = storm; // Инжектим сервис
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetProducts()
    {
        var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var user = await _db.Profiles
            .Where(u => u.Id == userId)
            .Select(u => new { u.LanguageCode })
            .FirstOrDefaultAsync();

        if (user == null) return Unauthorized();
        string lang = user.LanguageCode ?? "ru";

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
        var authJwt = User.FindFirst("ext_token")?.Value; // Достаем токен для Питона

        var product = await _db.Products.FindAsync(productId);
        if (product == null || !product.IsActive) return NotFound("Товар не найден");

        // --- ВАЖНО: СПИСАНИЕ БАЛАНСА В ПИТОНЕ ---
        if (!string.IsNullOrEmpty(authJwt))
        {
            // Передаем цену со знаком МИНУС
            bool success = await _storm.AddTransactionAsync(authJwt, -product.Price, $"Покупка: {product.Title}");

            if (!success)
            {
                return BadRequest(new { error = "Недостаточно средств или ошибка внешнего API" });
            }
        }

        var purchase = new Purchase
        {
            UserId = userId,
            ProductId = productId,
            PriceAtPurchase = product.Price,
            Status = "completed"
        };

        _db.Purchases.Add(purchase);
        await _db.SaveChangesAsync();

        return Ok(new { success = true });
    }
}