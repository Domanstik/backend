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

        var contests = await _db.Contests
            .Where(c => c.Language == lang || c.Language == "all")
            .Where(c => c.IsActive)
            .Include(c => c.Questions)
            .ThenInclude(q => q.Options)
            .OrderByDescending(c => c.EndDate)
            .ToListAsync();

        return Ok(contests);
    }

    // --- ЗАГРУЗКА РАБОТЫ ПОЛЬЗОВАТЕЛЕМ ---
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
            FileUrls = fileUrl,
            JoinedAt = DateTime.UtcNow
        };

        _db.ContestParticipants.Add(participant);
        await _db.SaveChangesAsync();

        return Ok(new { success = true, url = fileUrl });
    }

    // ==========================================================
    //                    АДМИНСКАЯ ЧАСТЬ
    // ==========================================================

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
                EndDate = DateTime.UtcNow.AddDays(7),
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
}