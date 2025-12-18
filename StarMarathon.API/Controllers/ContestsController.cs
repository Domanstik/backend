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
public class ContestsController : ControllerBase
{
    private readonly StarDbContext _db;
    private readonly IFileStorageService _files;

    // IAuthService убрали пока, так как не используем StormBV
    public ContestsController(StarDbContext db, IFileStorageService files)
    {
        _db = db;
        _files = files;
    }

    // --- USER AREA ---

    // GET: api/contests?lang=ru
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetList([FromQuery] string lang = "ru")
    {
        var list = await _db.Contests
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Where(c => c.Language == lang || c.Language == "all")
            .OrderByDescending(c => c.EndDate)
            .Select(c => new
            {
                c.Id,
                c.Kind,
                c.Title,
                c.Subtitle,
                c.StarsJoin,
                c.StarsWin,
                // Считаем дни (если дата есть)
                DaysLeft = c.EndDate.HasValue ? (c.EndDate.Value - DateTime.UtcNow).Days : 0,
                c.EndDate,
                // Проверяем, участвовал ли уже этот юзер (нужно знать ID юзера)
                // Пока упростим и вернем false, полноценно сделаем, когда достанем ID из токена
                Active = false
            })
            .ToListAsync();

        return Ok(list);
    }

    // GET: api/contests/{id}
    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetDetails(Guid id)
    {
        var item = await _db.Contests
            .Include(c => c.Questions)
                .ThenInclude(q => q.Options)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (item == null) return NotFound();
        return Ok(item);
    }

    // POST: api/contests/{id}/submit
    // Участие (загрузка файлов или ответы на опрос)
    [HttpPost("{id}/submit")]
    [Authorize]
    public async Task<IActionResult> Submit(Guid id, [FromForm] SubmitRequest req)
    {
        // Достаем ID юзера из токена
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(userIdString, out var userId))
            return Unauthorized();

        var contest = await _db.Contests.FindAsync(id);
        if (contest == null) return NotFound("Конкурс не найден");

        // Проверяем, не участвовал ли уже (если участие однократное)
        var exists = await _db.ContestParticipants
            .AnyAsync(p => p.ContestId == id && p.UserId == userId);

        if (exists) return BadRequest("Вы уже участвовали");

        // Создаем запись участника
        var participant = new ContestParticipant
        {
            ContestId = id,
            UserId = userId,
            JoinedAt = DateTime.UtcNow,
            AnswersJson = req.AnswersJson
        };

        // Загружаем файлы (если есть)
        if (req.Files != null && req.Files.Count > 0)
        {
            var urls = new List<string>();
            foreach (var file in req.Files)
            {
                // Грузим в бакет "contest-entries" (нужно создать его в Supabase или использовать "images")
                // Для простоты пока используем "images", но лучше создать отдельный
                var url = await _files.UploadFileAsync(file, "images");
                urls.Add(url);
            }
            participant.FileUrls = string.Join(";", urls);
        }

        _db.ContestParticipants.Add(participant);
        await _db.SaveChangesAsync();

        return Ok(new { success = true });
    }

    public record SubmitRequest(string? AnswersJson, List<IFormFile>? Files);


    // --- ADMIN AREA ---

    // GET: api/contests/admin/all
    [HttpGet("admin/all")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AdminGetAll()
    {
        var list = await _db.Contests
            .AsNoTracking()
            .OrderByDescending(c => c.Id) // Или CreatedAt
            .ToListAsync();
        return Ok(list);
    }

    // POST: api/contests (Создание)
    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] Contest contest)
    {
        // При создании нового ID генерируется автоматом, но можно форсировать
        contest.Id = Guid.NewGuid();

        // EF Core сам сохранит вложенные Questions и Options
        _db.Contests.Add(contest);
        await _db.SaveChangesAsync();

        return Ok(contest);
    }

    // PUT: api/contests/{id} (Обновление)
    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Contest updated)
    {
        var existing = await _db.Contests
            .Include(c => c.Questions).ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (existing == null) return NotFound();

        // Обновляем простые поля
        existing.Title = updated.Title;
        existing.Subtitle = updated.Subtitle;
        existing.Kind = updated.Kind;
        existing.Language = updated.Language;
        existing.Location = updated.Location;
        existing.StarsJoin = updated.StarsJoin;
        existing.StarsWin = updated.StarsWin;
        existing.EndDate = updated.EndDate;
        existing.IsActive = updated.IsActive;

        // Обновление вопросов (сложная логика: удалить старые, добавить новые)
        // Для простоты: удаляем все старые вопросы и добавляем новые из запроса
        _db.ContestQuestions.RemoveRange(existing.Questions);

        // Добавляем новые (EF Core создаст новые ID)
        foreach (var q in updated.Questions)
        {
            q.Id = Guid.NewGuid(); // Сброс ID, чтобы создались новые
            q.ContestId = existing.Id;
            foreach (var opt in q.Options) opt.Id = Guid.NewGuid();
            existing.Questions.Add(q);
        }

        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    // DELETE: api/contests/{id}
    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await _db.Contests.FindAsync(id);
        if (item == null) return NotFound();

        _db.Contests.Remove(item);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // GET: api/contests/admin/{id}/participants
    [HttpGet("admin/{id}/participants")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetParticipants(Guid id)
    {
        var parts = await _db.ContestParticipants
            .Include(p => p.User)
            .Where(p => p.ContestId == id)
            .OrderByDescending(p => p.JoinedAt)
            .ToListAsync();

        var result = parts.Select(p => new
        {
            p.Id,
            UserId = p.User.Id,
            UserName = p.User.Username ?? "User " + p.User.Id,
            AvatarUrl = p.User.AvatarUrl,
            p.JoinedAt,
            p.IsWinner,
            // Превращаем строку с путями в массив
            Files = string.IsNullOrEmpty(p.FileUrls) ? new string[0] : p.FileUrls.Split(';', StringSplitOptions.RemoveEmptyEntries),
            // Ответы отдаем как есть (строка JSON), фронт распарсит
            Answers = p.AnswersJson
        });

        return Ok(result);
    }
}