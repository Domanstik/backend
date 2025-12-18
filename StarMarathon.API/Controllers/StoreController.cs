using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StarMarathon.Application.Interfaces;
using StarMarathon.Domain.Entities;
using StarMarathon.Infrastructure.Persistence;
using System.Security.Claims; // Нужно для User.FindFirst

namespace StarMarathon.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StoreController : ControllerBase
{
    private readonly StarDbContext _db;
    private readonly IFileStorageService _fileService;
    private readonly IAuthService _authService; // <--- 1. Добавили поле

    // 2. Добавили сервис в конструктор
    public StoreController(StarDbContext db, IFileStorageService fileService, IAuthService authService)
    {
        _db = db;
        _fileService = fileService;
        _authService = authService;
    }

    // GET: api/store (Получить товары для текущего юзера)
    [HttpGet]
    // [Authorize] 
    public async Task<IActionResult> GetProducts()
    {
        string userLang = "ru";
        // В будущем можно брать из User.FindFirst("language")?.Value

        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Where(p => p.Language == userLang || p.Language == "all")
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return Ok(products);
    }

    // POST: api/store (Создать товар - Админ)
    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateProduct([FromForm] CreateProductRequest req)
    {
        string? imageUrl = null;

        if (req.Image != null)
        {
            imageUrl = await _fileService.UploadFileAsync(req.Image, "images");
        }

        var product = new Product
        {
            Title = req.Title,
            Description = req.Description ?? "",
            Price = req.Price,
            Language = req.Language ?? "ru",
            ImageUrl = imageUrl,
            IsActive = true
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        return Ok(product);
    }

    // POST: api/store/buy
    [HttpPost("buy")]
    [Authorize]
    public async Task<IActionResult> Buy([FromBody] BuyRequest req)
    {
        var product = await _db.Products.FindAsync(req.ProductId);
        if (product == null || !product.IsActive)
            return BadRequest("Товар не найден");

        var extToken = User.FindFirst("ext_token")?.Value;
        var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        // 1. Списываем деньги (отрицательная сумма)
        var success = await _authService.AddTransactionAsync(extToken, -product.Price, $"Покупка: {product.Title}");

        if (!success) return BadRequest("Недостаточно звезд или ошибка сервиса");

        // 2. Сохраняем в историю Supabase
        var purchase = new Purchase
        {
            UserId = userId,
            ProductId = product.Id,
            PriceAtPurchase = product.Price,
            Status = "pending"
        };
        _db.Purchases.Add(purchase);
        await _db.SaveChangesAsync();

        return Ok(new { success = true });
    }

    // DTO
    public record CreateProductRequest(string Title, int Price, string? Description, string? Language, IFormFile? Image);
    public record BuyRequest(Guid ProductId);
}