namespace Hexara.Application.Common.Interfaces;

/// <summary>نام و ظاهر یک کاربر — چیزی که دامنه‌ی بازی نمی‌داند و نباید بداند.</summary>
public sealed record PlayerProfile(Guid Id, string DisplayName, string AvatarColor, bool IsGuest);

/// <summary>
/// خواندن مشخصات نمایشی چند کاربر با هم. جدا از مخزن بازی نگه داشته شده تا
/// لایه‌ی بازی به Identity گره نخورد.
/// </summary>
public interface IPlayerDirectory
{
    Task<IReadOnlyList<PlayerProfile>> GetAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken = default);
}
