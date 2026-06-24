using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Domain.Entities;

/// <summary>A single turn in a conversation about a document.</summary>
public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
