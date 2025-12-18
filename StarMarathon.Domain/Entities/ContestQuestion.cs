namespace StarMarathon.Domain.Entities;

public class ContestQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ContestId { get; set; }
    public Contest Contest { get; set; } = null!;

    public string Title { get; set; } = string.Empty; // Текст вопроса
    public int Stars { get; set; }

    public bool Multiple { get; set; }      // Можно ли несколько вариантов
    public bool IsTextField { get; set; }   // Это текстовый ответ?
    public string? CorrectTextAnswer { get; set; } // Правильный ответ (для текста)

    public List<ContestAnswerOption> Options { get; set; } = new();
}