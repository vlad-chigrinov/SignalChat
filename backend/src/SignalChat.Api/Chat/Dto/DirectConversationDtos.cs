namespace SignalChat.Api.Chat.Dto;

public sealed record DirectConversationDto(Guid Id, UserSummaryDto Peer, LastMessageDto? LastMessage)
{
    public string Kind => KindName;

    public const string KindName = "direct";
}

public sealed record CreateDirectConversationRequest(Guid UserId);
