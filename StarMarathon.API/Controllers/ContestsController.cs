using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StarMarathon.Domain.Entities;
using StarMarathon.Infrastructure.Persistence;
using StarMarathon.Application.Interfaces;
using System.Security.Claims;

namespace StarMarathon.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ContestsController : ControllerBase
{
    private readonly StarDbContext _db;
    private readonly IFileStorageService _fileStorage;

    public ContestsController(StarDbContext db, IFileStorageService fileStorage)
    {
        _db = db;
        _fileStorage = fileStorage;
    }

    [HttpGet]
    public async Task<IActionResult> GetContests()
    {
        var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        // ИСПРАВЛЕНИЕ: Сначала фильтруем по ID, потом выбираем поле
        var user = await _db.Profiles
            .Where(u => u.Id == userId) // <--- Фильтр здесь
            .Select(u => new { u.LanguageCode }) // <--- Проекция здесь
            .FirstOrDefaultAsync();

        if (user == null) return Unauthorized();
        string lang = user.LanguageCode ?? "ru";

        var contests = await _db.Contests
            .Where(c => c.Language == lang || c.Language == "all")
            .Where(c => c.IsActive)
            .Include(c => c.Questions)
            .ThenInclude(q => q.Options)
            .OrderByDescending(c => c.EndDate) // Используем EndDate для сортировки
            .ToListAsync();

        return Ok(contests);
    }

    // Загрузка работы (фото/видео)
    [HttpPost("{contestId}/upload")]
    public async Task<IActionResult> UploadEntry(Guid contestId, IFormFile file)
    {
        var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var contest = await _db.Contests.FindAsync(contestId);
        if (contest == null) return NotFound();

        var fileUrl = await _fileStorage.UploadFileAsync(file, "contest-entries");

        var participant = new ContestParticipant
        {
            ContestId = contestId,
            UserId = userId,
            FileUrls = fileUrl, // Используем поле FileUrls (множественное число, как в вашей модели)
            JoinedAt = DateTime.UtcNow
        };

        _db.ContestParticipants.Add(participant);
        await _db.SaveChangesAsync();

        return Ok(new { success = true, url = fileUrl });
    }
}