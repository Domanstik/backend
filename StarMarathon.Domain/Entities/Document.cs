namespace StarMarathon.Domain.Entities;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string TextContent { get; set; } = string.Empty;
    public string Language { get; set; } = "ru";
}