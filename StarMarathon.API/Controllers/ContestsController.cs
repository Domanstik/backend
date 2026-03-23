using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StarMarathon.Domain.Entities;
using StarMarathon.Infrastructure.Persistence;
using StarMarathon.Application.Interfaces;
using System.Security.Claims;
using System.Text.Json.Serialization; // <-- ДОБАВИЛИ ДЛЯ ТОЧНОГО МАППИНГА JSON

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
                Kind = req.Kind ?? "contest",
                Title = req.Title ?? "Без названия",
                Subtitle = req.Subtitle ?? "",
                Language = req.Language?.ToLower() ?? "ru",
                Location = req.Location ?? "All",
                StarsJoin = req.StarsJoin,
                StarsWin = req.StarsWin,
                IsActive = req.IsActive,
                EndDate = DateTime.UtcNow.AddDays(7),
                Questions = new List<ContestQuestion>()
            };

            _db.Contests.Add(contest);
            await _db.SaveChangesAsync();

            return Ok(new { success = true, id = contest.Id });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // --- НОВЫЙ МЕТОД: УДАЛЕНИЕ КОНКУРСА ---
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

// --- БРОНЕБОЙНЫЙ DTO ---
public class CreateContestDto
{
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("starsJoin")]
    public int StarsJoin { get; set; }

    [JsonPropertyName("starsWin")]
    public int StarsWin { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("questions")]
    public List<object>? Questions { get; set; }
}