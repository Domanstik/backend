using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StarMarathon.Domain.Entities;
using StarMarathon.Infrastructure.Persistence;

namespace StarMarathon.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly StarDbContext _db;

    public DocumentsController(StarDbContext db)
    {
        _db = db;
    }

    // GET: api/documents?lang=ru
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string lang = "ru")
    {
        var docs = await _db.Documents
            .AsNoTracking()
            .Where(d => d.Language == lang)
            .ToListAsync();

        return Ok(docs);
    }

    // POST: api/documents (Создание/Обновление)
    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Save([FromBody] Document doc)
    {
        // Простая логика: если прислали ID - обновляем, нет - создаем
        var existing = await _db.Documents.FindAsync(doc.Id);

        if (existing != null)
        {
            existing.Title = doc.Title;
            existing.TextContent = doc.TextContent;
            existing.Language = doc.Language;
        }
        else
        {
            doc.Id = Guid.NewGuid();
            _db.Documents.Add(doc);
        }

        await _db.SaveChangesAsync();
        return Ok(doc);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var doc = await _db.Documents.FindAsync(id);
        if (doc == null) return NotFound();

        _db.Documents.Remove(doc);
        await _db.SaveChangesAsync();
        return Ok();
    }
}