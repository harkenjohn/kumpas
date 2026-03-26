namespace Kumpas.AdminWeb.ViewModels;

public class ConversationHistoryViewModel
{
    public string? Search { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public IReadOnlyList<ConversationSessionRowViewModel> Sessions { get; set; } = [];
}

public class ConversationSessionRowViewModel
{
    public Guid Id { get; set; }
    public string RoomCode { get; set; } = "N/A";
    public string ParticipantOne { get; set; } = string.Empty;
    public string ParticipantTwo { get; set; } = string.Empty;
    public int MessageCount { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? LastMessageAt { get; set; }
}

public class ConversationDetailsViewModel
{
    public Guid Id { get; set; }
    public string RoomCode { get; set; } = "N/A";
    public string ParticipantOne { get; set; } = string.Empty;
    public string ParticipantTwo { get; set; } = string.Empty;
    public DateTimeOffset? CreatedAt { get; set; }
    public IReadOnlyList<ConversationMessageRowViewModel> Messages { get; set; } = [];
}

public class ConversationMessageRowViewModel
{
    public long Id { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string MessageContent { get; set; } = string.Empty;
    public DateTimeOffset? CreatedAt { get; set; }
}
