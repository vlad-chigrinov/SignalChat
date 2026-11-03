using SignalChat.Api.Chat.Dto;
using SignalChat.Api.Chat.Photos;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Chat;

namespace SignalChat.Api.Chat;

/// <summary>Turns infrastructure views into the JSON shapes the frontend expects.</summary>
public static class ChatMapping
{
    public static UserSummaryDto ToDto(ChatUser user) => new(user.Id, user.DisplayName, user.AvatarUrl);

    public static LastMessageDto? ToDto(LastMessageView? message) =>
        message is null ? null : new LastMessageDto(message.AuthorId, message.Text, message.SentAt, message.PhotoCount);

    public static RoomDto ToDto(RoomSummary room) =>
        new(room.Id, room.Name, room.Description, room.MemberCount, ToDto(room.LastMessage));

    public static RoomMemberDto ToDto(RoomMemberView member) =>
        new(ToDto(member.User), member.Role == RoomRole.Owner ? RoomRoleNames.Owner : RoomRoleNames.Member);

    public static DirectConversationDto ToDto(DirectConversationSummary conversation) =>
        new(conversation.Id, ToDto(conversation.Peer), ToDto(conversation.LastMessage), ToStatusName(conversation.Status));

    public static DirectRequestDto ToDto(IncomingDirectRequestView request) =>
        new(request.Id, ToDto(request.From), request.Text, request.CreatedAt);

    private static string ToStatusName(DirectConversationStatus status) =>
        status == DirectConversationStatus.Accepted
            ? DirectConversationStatusNames.Accepted
            : DirectConversationStatusNames.Pending;

    public static MessageDto ToDto(MessageView message) =>
        new(message.Id, message.ConversationId, ToDto(message.Author), message.Text, message.SentAt,
            message.Attachments.Select(ToDto).ToList(), ToDtos(message.Reactions));

    public static AttachmentDto ToDto(AttachmentView attachment) =>
        new(attachment.Id, attachment.Width, attachment.Height, attachment.ThumbnailWidth, attachment.ThumbnailHeight,
            attachment.SizeBytes, attachment.ContentType, attachment.FileName,
            AttachmentRoutes.PathOf(attachment.Id, AttachmentVariant.Original),
            AttachmentRoutes.PathOf(attachment.Id, AttachmentVariant.Thumbnail));

    public static ReactionDto ToDto(ReactionView reaction) => new(reaction.Emoji, reaction.Count, reaction.ReactedByMe);

    public static IReadOnlyList<ReactionDto> ToDtos(IReadOnlyList<ReactionView> reactions) =>
        reactions.Select(ToDto).ToList();

    public static MessagePageDto ToDto(MessagePage page) =>
        new(page.Items.Select(ToDto).ToList(), page.NextCursor);
}
