using Hexara.Application.Rooms;

namespace Hexara.Application.Common.Interfaces;

/// <summary>ذخیره و بازیابی اتاق‌های پیش از بازی.</summary>
public interface IRoomRepository
{
    Task<Room?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<Room?> FindByIdAsync(Guid roomId, CancellationToken cancellationToken = default);

    /// <summary>اتاق تازه با میزبانی که خودش روی صندلی صفر می‌نشیند.</summary>
    Task<Room> CreateAsync(
        Guid hostId,
        string code,
        RoomSettings settings,
        CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>نشستن روی یک صندلی مشخص. اگر صندلی همزمان پر شده باشد <c>false</c>.</summary>
    Task<bool> AddMemberAsync(
        Guid roomId,
        Guid userId,
        int seat,
        CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(Guid roomId, Guid userId, CancellationToken cancellationToken = default);

    Task UpdateSettingsAsync(Guid roomId, RoomSettings settings, CancellationToken cancellationToken = default);

    Task SetHostAsync(Guid roomId, Guid hostId, CancellationToken cancellationToken = default);

    Task SetStatusAsync(Guid roomId, RoomStatus status, CancellationToken cancellationToken = default);

    /// <summary>اتاق را به بازیِ ساخته‌شده گره می‌زند و وضعیتش را «شروع‌شده» می‌کند.</summary>
    Task AttachGameAsync(Guid roomId, Guid gameId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoomSummary>> ListOpenAsync(int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoomSummary>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
