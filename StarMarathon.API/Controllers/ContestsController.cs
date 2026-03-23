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

    // Создание нового конкурса (ТОЛЬКО ДЛЯ АДМИНОВ)
    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<IActionResult> CreateContest([FromBody] CreateContestDto req)
    {
        try
        {
            // 1. Создаем сущность конкурса
            var contest = new Contest
            {
                Id = Guid.NewGuid(),
                Kind = req.Kind ?? "contest",
                Title = req.Title,
                Subtitle = req.Subtitle,
                Language = req.Language?.ToLower() ?? "ru",
                Location = req.Location ?? "All",
                StarsJoin = req.StarsJoin,
                StarsWin = req.StarsWin,
                IsActive = req.IsActive,
                CreatedAt = DateTime.UtcNow,
                // Если у тебя в модели EndDate, можно распарсить из Subtitle или оставить null
                EndDate = DateTime.UtcNow.AddDays(7)
            };

            // 2. Если есть вопросы, добавляем их (если твоя модель это поддерживает)
            if (req.Questions != null && req.Questions.Any())
            {
                // Тут логика добавления вопросов, если нужно
            }

            _db.Contests.Add(contest);
            await _db.SaveChangesAsync();

            return Ok(new { success = true, id = contest.Id });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

// Вспомогательный класс (DTO) для приема данных с фронта
public record CreateContestDto(
    string Kind,
    string Title,
    string Subtitle,
    string Language,
    string Location,
    int StarsJoin,
    int StarsWin,
    bool IsActive,
    List<object> Questions // Пока object, если структура вопросов сложная
);