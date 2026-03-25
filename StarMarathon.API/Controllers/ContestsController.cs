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

    // --- ПОЛУЧЕНИЕ КОНКУРСОВ ДЛЯ ОБЫЧНЫХ ПОЛЬЗОВАТЕЛЕЙ ---
    [HttpGet]
    public async Task<IActionResult> GetContests()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null) return Unauthorized();

        var userId = long.Parse(userIdClaim.Value);

        var user = await _db.Profiles
            .Where(u => u.Id == userId)
            .Select(u => new { u.LanguageCode })
            .FirstOrDefaultAsync();

        if (user == null) return Unauthorized();
        string lang = user.LanguageCode ?? "ru";

        // 1. Достаем список конкурсов (вместе с вопросами)
        var contests = await _db.Contests
            .Where(c => c.Language == lang || c.Language == "all")
            .Where(c => c.IsActive)
            .Include(c => c.Questions)
                .ThenInclude(q => q.Options)
            .OrderByDescending(c => c.EndDate)
            .ToListAsync();

        // 2. Собираем ID найденных конкурсов
        var contestIds = contests.Select(c => c.Id).ToList();

        // 3. Достаем из базы участия ТЕКУЩЕГО юзера в ЭТИХ конкурсах
        var userParticipations = await _db.ContestParticipants
            .Where(p => p.UserId == userId && contestIds.Contains(p.ContestId))
            .ToListAsync();

        // 4. "Склеиваем" данные, формируя ответ для фронтенда
        var result = contests.Select(c => new
        {
            c.Id,
            c.Kind,
            c.Title,
            c.Subtitle,
            c.Language,
            c.Location,
            c.StarsJoin,
            c.StarsWin,
            c.EndDate,
            c.IsActive,
            c.Questions,
            // Фронтенд увидит это как поле 'contestParticipants' (массив участий)
            ContestParticipants = userParticipations.Where(p => p.ContestId == c.Id).ToList()
        });

        return Ok(result);
    }

    // --- ЗАГРУЗКА РАБОТЫ ПОЛЬЗОВАТЕЛЕМ (БРОНЕБОЙНАЯ ВЕРСИЯ) ---
    [HttpPost("{contestId}/upload")]
    public async Task<IActionResult> UploadEntry(Guid contestId, [FromForm] IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "Файл не получен сервером. Попробуйте выбрать другое фото." });

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized(new { error = "Пользователь не авторизован" });

            var userId = long.Parse(userIdClaim.Value);
            var contest = await _db.Contests.FindAsync(contestId);

            if (contest == null)
                return NotFound(new { error = "Конкурс не найден" });

            // Сохраняем файл
            var fileUrl = await _fileStorage.UploadFileAsync(file, "contest-entries");

            var participant = new ContestParticipant
            {
                ContestId = contestId,
                UserId = userId,
                FileUrls = fileUrl,
                JoinedAt = DateTime.UtcNow
            };

            _db.ContestParticipants.Add(participant);
            await _db.SaveChangesAsync();

            return Ok(new { success = true, url = fileUrl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Внутренняя ошибка сервера: " + ex.Message });
        }
    }

    // ==========================================================
    //                    АДМИНСКАЯ ЧАСТЬ
    // ==========================================================

    // --- ПОЛУЧЕНИЕ УЧАСТНИКОВ КОНКУРСА ДЛЯ АДМИНКИ ---
    [Authorize(Roles = "admin")]
    [HttpGet("admin/{contestId}/participants")]
    public async Task<IActionResult> GetContestParticipants(Guid contestId)
    {
        try
        {
            var participants = await _db.ContestParticipants
                .Where(p => p.ContestId == contestId)
                .OrderByDescending(p => p.JoinedAt)
                .ToListAsync();

            return Ok(participants);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // --- ПОЛУЧЕНИЕ ВСЕХ КОНКУРСОВ ДЛЯ АДМИНКИ (без фильтров) ---
    [Authorize(Roles = "admin")]
    [HttpGet("admin/all")]
    public async Task<IActionResult> GetAllForAdmin()
    {
        try
        {
            var contests = await _db.Contests
                .Include(c => c.Questions)
                .ThenInclude(q => q.Options)
                .OrderByDescending(c => c.EndDate)
                .ToListAsync();

            return Ok(contests);
        }
        catch (Exception ex)
        {
            // Исправлена опечатка со знаком =
            return BadRequest(new { error = ex.Message });
        }
    }

    // --- СОЗДАНИЕ КОНКУРСА ---
    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<IActionResult> CreateContest([FromBody] CreateContestDto req)
    {
        try
        {
            var contest = new Contest
            {
                Id = Guid.NewGuid(),
                Kind = req.kind ?? "contest",
                Title = req.title ?? "Без названия",
                Subtitle = req.subtitle ?? "",
                Language = req.language?.ToLower() ?? "ru",
                Location = req.location ?? "All",
                StarsJoin = req.starsJoin,
                StarsWin = req.starsWin,
                IsActive = req.isActive,
                EndDate = req.endDate ?? DateTime.UtcNow.AddDays(7),
                Questions = new List<ContestQuestion>()
            };

            _db.Contests.Add(contest);
            await _db.SaveChangesAsync();

            return Ok(new { success = true, id = contest.Id, receivedTitle = req.title });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // --- ОБНОВЛЕНИЕ КОНКУРСА (Редактирование) ---
    [Authorize(Roles = "admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateContest(Guid id, [FromBody] CreateContestDto req)
    {
        try
        {
            var contest = await _db.Contests.FindAsync(id);
            if (contest == null) return NotFound(new { error = "Конкурс не найден" });

            if (req.title != null) contest.Title = req.title;
            if (req.subtitle != null) contest.Subtitle = req.subtitle;
            if (req.language != null) contest.Language = req.language.ToLower();
            if (req.location != null) contest.Location = req.location;
            if (req.kind != null) contest.Kind = req.kind;

            contest.StarsJoin = req.starsJoin;
            contest.StarsWin = req.starsWin;
            contest.IsActive = req.isActive;

            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // --- УДАЛЕНИЕ КОНКУРСА ---
    [Authorize(Roles = "admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteContest(Guid id)
    {
        try
        {
            var contest = await _db.Contests.FindAsync(id);
            if (contest == null) return NotFound(new { error = "Конкурс не найден" });

            _db.Contests.Remove(contest);
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

// 100% надежный DTO: имена свойств в точности копируют JSON от React
public class CreateContestDto
{
    public string? kind { get; set; }
    public string? title { get; set; }
    public string? subtitle { get; set; }
    public string? language { get; set; }
    public string? location { get; set; }
    public int starsJoin { get; set; }
    public int starsWin { get; set; }
    public bool isActive { get; set; }

    public DateTime? endDate { get; set; }
}