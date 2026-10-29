namespace SignalChat.Api.Chat.Dto;

public sealed record RoomDto(Guid Id, string Name, string Description, int MemberCount, LastMessageDto? LastMessage)
{
    /// <summary>Discriminator shared with <see cref="DirectConversationDto"/>: rooms and DMs share one id space.</summary>
    public string Kind => KindName;

    public const string KindName = "room";
}

public sealed record CreateRoomRequest(string? Name, string? Description);
