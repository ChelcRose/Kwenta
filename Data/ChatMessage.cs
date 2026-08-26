namespace Kwenta.Data;

public enum ChatMessageRole
{
    User,
    Assistant
}

public class ChatMessage
{
    public int Id { get; set; }

    public int ChatConversationId { get; set; }

    public ChatConversation ChatConversation { get; set; } = null!;

    public ChatMessageRole Role { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
