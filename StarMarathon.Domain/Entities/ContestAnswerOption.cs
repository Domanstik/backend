namespace StarMarathon.Domain.Entities;

public class ContestAnswerOption
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid QuestionId { get; set; }
    public ContestQuestion Question { get; set; } = null!;

    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; } // Правильный ли это вариант
}