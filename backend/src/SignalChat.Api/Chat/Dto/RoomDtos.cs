namespace SignalChat.Api.Chat.Dto;

public sealed record RoomDto(Guid Id, string Name, string Description, int MemberCount, LastMessageDto? LastMessage)
{
    /// <summary>Discriminator shared with <see cref="DirectConversationDto"/>: rooms and DMs share one id space.</summary>
    public string Kind => KindName;

    public const string KindName = "room";
}

public sealed record CreateRoomRequest(string? Name, string? Description);

/// <summary>A member of a room: the current public profile (no email) and the role.</summary>
public sealed record RoomMemberDto(UserSummaryDto User, string Role);

/// <summary>Wire values of <see cref="RoomMemberDto.Role"/>.</summary>
public static class RoomRoleNames
{
    public const string Owner = "owner";

    public const string Member = "member";
}

/// <summary>Adds <see cref="UserId"/> to the room. Allowed only for a chat partner of the caller.</summary>
public sealed record InviteToRoomRequest(Guid UserId);
