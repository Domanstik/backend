namespace StarMarathon.Domain.Entities;

public class Contest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Kind { get; set; } = "contest"; // "contest" или "survey"
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }

    public string Language { get; set; } = "ru";
    public string Location { get; set; } = "All";

    public int StarsJoin { get; set; }
    public int StarsWin { get; set; }

    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;

    // Связь с вопросами
    public List<ContestQuestion> Questions { get; set; } = new();
}