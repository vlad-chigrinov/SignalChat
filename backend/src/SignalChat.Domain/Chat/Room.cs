namespace SignalChat.Domain.Chat;

/// <summary>
/// A public room: anyone can join it, but its content is visible to members only.
/// <see cref="NormalizedName"/> is the unique key, so names are unique regardless of case.
/// </summary>
public sealed class Room
{
    public const int NameMinLength = 3;
    public const int NameMaxLength = 80;
    public const int DescriptionMaxLength = 300;
    public const int MaxOwnedRoomsPerUser = 20;

    private Room()
    {
        // Required by EF Core.
        Name = NormalizedName = Description = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    public string Description { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public static Room Create(string? name, string? description, Guid creatorId, DateTimeOffset now)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        if (trimmedName.Length is < NameMinLength or > NameMaxLength)
        {
            throw new ChatRuleException("invalid_room_name", $"Room name must be {NameMinLength}..{NameMaxLength} characters.");
        }

        var trimmedDescription = (description ?? string.Empty).Trim();
        if (trimmedDescription.Length > DescriptionMaxLength)
        {
            throw new ChatRuleException("invalid_room_description", $"Room description must be at most {DescriptionMaxLength} characters.");
        }

        return new Room
        {
            Id = Guid.CreateVersion7(now),
            Name = trimmedName,
            NormalizedName = trimmedName.ToLowerInvariant(),
            Description = trimmedDescription,
            CreatedAt = now,
            CreatedByUserId = creatorId,
        };
    }
}
