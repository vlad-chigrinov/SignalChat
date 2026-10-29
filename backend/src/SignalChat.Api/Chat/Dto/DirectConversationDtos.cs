namespace SignalChat.Api.Chat.Dto;

/// <summary>
/// A direct conversation as its caller sees it. <see cref="Status"/> is "accepted" for a chat, and "pending" for a request
/// the caller sent (a declined request is shown as pending to its initiator, never as declined).
/// </summary>
public sealed record DirectConversationDto(Guid Id, UserSummaryDto Peer, LastMessageDto? LastMessage, string Status)
{
    public string Kind => KindName;

    public const string KindName = "direct";
}

/// <summary>Wire values of <see cref="DirectConversationDto.Status"/>.</summary>
public static class DirectConversationStatusNames
{
    public const string Accepted = "accepted";

    public const string Pending = "pending";
}

/// <summary>Asks to write to <see cref="UserId"/>. <see cref="Text"/> is the request's first message (required for a new request).</summary>
public sealed record CreateDirectConversationRequest(Guid UserId, string? Text);

/// <summary>A request waiting for the caller's answer. <see cref="Id"/> is the conversation id to accept or decline.</summary>
public sealed record DirectRequestDto(Guid Id, UserSummaryDto From, string Text, DateTimeOffset CreatedAt);
