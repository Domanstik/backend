namespace StarMarathon.Domain.Entities;

public class ContestParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // В каком конкурсе/опросе участвует
    public Guid ContestId { get; set; }
    public Contest Contest { get; set; } = null!;

    // Кто участвует
    public long UserId { get; set; }
    public UserProfile User { get; set; } = null!;

    // Ссылки на загруженные файлы (для конкурсов).
    // Можно хранить через точку с запятой: "url1;url2"
    public string? FileUrls { get; set; }

    // Ответы на вопросы (для опросов).
    // Храним как JSON строку: [{"qId": "...", "answer": "..."}]
    public string? AnswersJson { get; set; }

    // Является ли победителем (выбирает админ)
    public bool IsWinner { get; set; } = false;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}