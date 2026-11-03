using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SignalChat.Api.Tests.Infrastructure;
using SignalChat.Domain.Chat;
using SignalChat.Infrastructure.Persistence;

namespace SignalChat.Api.Tests.Chat;

/// <summary>Direct database access for the reaction tests: what the REST surface cannot do or show.</summary>
internal static class ReactionDatabase
{
    public static async Task SoftDeleteAsync(SignalChatApiFactory factory, Guid messageId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SignalChatDbContext>();
        var message = await db.Messages.SingleAsync(item => item.Id == messageId);
        message.Delete(factory.Clock.GetUtcNow());
        await db.SaveChangesAsync();
    }

    /// <summary>The oldest message of a conversation, e.g. the text of a direct request.</summary>
    public static async Task<Guid> FirstMessageIdAsync(SignalChatApiFactory factory, Guid conversationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SignalChatDbContext>();
        return await db.Messages.AsNoTracking()
            .Where(message => message.DirectConversationId == conversationId || message.RoomId == conversationId)
            .OrderBy(message => message.CreatedAt)
            .Select(message => message.Id)
            .FirstAsync();
    }

    /// <summary>Saves a reaction row without going through the store, to test what the database itself refuses.</summary>
    public static async Task InsertAsync(SignalChatApiFactory factory, Guid messageId, Guid userId, string emoji)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SignalChatDbContext>();
        db.MessageReactions.Add(MessageReaction.Create(messageId, userId, emoji, factory.Clock.GetUtcNow()));
        await db.SaveChangesAsync();
    }
}
