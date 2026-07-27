using System.Security.Cryptography;
using Hexara.Application.Common.Interfaces;
using Hexara.Application.Games;

namespace Hexara.Application.Rooms;

/// <summary>دلیل رد شدن یک عملیات روی اتاق — کددار، تا لایه‌ی وب به کلید ترجمه نگاشتش کند.</summary>
public enum RoomError
{
    None = 0,
    RoomNotFound,
    RoomClosed,
    RoomAlreadyStarted,
    RoomFull,
    AlreadyJoined,
    NotAMember,
    NotHost,
    NotEnoughPlayers,
    InvalidSettings,
    SeatTaken
}

public sealed record RoomResult(RoomError Error, Room? Room = null, Guid? GameId = null)
{
    public bool Success => Error == RoomError.None;

    public static RoomResult Fail(RoomError error) => new(error);

    public static RoomResult Ok(Room room) => new(RoomError.None, room);
}

/// <summary>
/// اتاق‌های پیش از بازی: ساخت، پیوستن با کد، ترک، تغییر تنظیمات و شروع بازی.
/// </summary>
public sealed class RoomService
{
    private const int MaxCodeAttempts = 10;

    private readonly IRoomRepository _rooms;
    private readonly GameService _games;

    public RoomService(IRoomRepository rooms, GameService games)
    {
        _rooms = rooms;
        _games = games;
    }

    public async Task<RoomResult> CreateAsync(
        Guid hostId,
        RoomSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (!settings.IsValid)
        {
            return RoomResult.Fail(RoomError.InvalidSettings);
        }

        var code = await NewCodeAsync(cancellationToken);
        var room = await _rooms.CreateAsync(hostId, code, settings, cancellationToken);

        return RoomResult.Ok(room);
    }

    public Task<Room?> FindAsync(string code, CancellationToken cancellationToken = default) =>
        _rooms.FindByCodeAsync(RoomCode.Normalize(code), cancellationToken);

    public Task<Room?> FindByIdAsync(Guid roomId, CancellationToken cancellationToken = default) =>
        _rooms.FindByIdAsync(roomId, cancellationToken);

    public Task<IReadOnlyList<RoomSummary>> ListOpenAsync(
        int limit = 30,
        CancellationToken cancellationToken = default) =>
        _rooms.ListOpenAsync(limit, cancellationToken);

    public Task<IReadOnlyList<RoomSummary>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _rooms.ListForUserAsync(userId, cancellationToken);

    public async Task<RoomResult> JoinAsync(
        string code,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var room = await _rooms.FindByCodeAsync(RoomCode.Normalize(code), cancellationToken);
        if (room is null)
        {
            return RoomResult.Fail(RoomError.RoomNotFound);
        }

        // پیوستن دوباره خطا نیست: بازگشت به همان اتاق باید بی‌دردسر باشد.
        if (room.Contains(userId))
        {
            return RoomResult.Ok(room);
        }

        if (room.Status == RoomStatus.Started)
        {
            return RoomResult.Fail(RoomError.RoomAlreadyStarted);
        }

        if (room.Status == RoomStatus.Closed)
        {
            return RoomResult.Fail(RoomError.RoomClosed);
        }

        var seat = room.FirstFreeSeat();
        if (seat < 0)
        {
            return RoomResult.Fail(RoomError.RoomFull);
        }

        // اگر همین لحظه کس دیگری همان صندلی را گرفته باشد، درج شکست می‌خورد.
        if (!await _rooms.AddMemberAsync(room.Id, userId, seat, cancellationToken))
        {
            return RoomResult.Fail(RoomError.SeatTaken);
        }

        return RoomResult.Ok((await _rooms.FindByIdAsync(room.Id, cancellationToken))!);
    }

    /// <summary>
    /// ترک اتاق. اگر میزبان برود، میزبانی به نفر بعدی می‌رسد؛ اگر کسی نماند اتاق
    /// بسته می‌شود تا در فهرست لابی زباله نماند.
    /// </summary>
    public async Task<RoomResult> LeaveAsync(
        Guid roomId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var room = await _rooms.FindByIdAsync(roomId, cancellationToken);
        if (room is null)
        {
            return RoomResult.Fail(RoomError.RoomNotFound);
        }

        if (!room.Contains(userId))
        {
            return RoomResult.Fail(RoomError.NotAMember);
        }

        if (room.Status == RoomStatus.Started)
        {
            return RoomResult.Fail(RoomError.RoomAlreadyStarted);
        }

        await _rooms.RemoveMemberAsync(roomId, userId, cancellationToken);

        var remaining = room.Members.Where(m => m.UserId != userId).ToList();
        if (remaining.Count == 0)
        {
            await _rooms.SetStatusAsync(roomId, RoomStatus.Closed, cancellationToken);
        }
        else if (room.IsHost(userId))
        {
            await _rooms.SetHostAsync(roomId, remaining[0].UserId, cancellationToken);
        }

        return new RoomResult(RoomError.None, await _rooms.FindByIdAsync(roomId, cancellationToken));
    }

    public async Task<RoomResult> UpdateSettingsAsync(
        Guid roomId,
        Guid userId,
        RoomSettings settings,
        CancellationToken cancellationToken = default)
    {
        var room = await _rooms.FindByIdAsync(roomId, cancellationToken);
        if (room is null)
        {
            return RoomResult.Fail(RoomError.RoomNotFound);
        }

        if (!room.IsHost(userId))
        {
            return RoomResult.Fail(RoomError.NotHost);
        }

        if (room.Status != RoomStatus.Open)
        {
            return RoomResult.Fail(RoomError.RoomAlreadyStarted);
        }

        // سقف صندلی را نمی‌توان زیر تعداد کسانی که نشسته‌اند آورد.
        if (!settings.IsValid || settings.MaxPlayers < room.Members.Count)
        {
            return RoomResult.Fail(RoomError.InvalidSettings);
        }

        await _rooms.UpdateSettingsAsync(roomId, settings, cancellationToken);

        return new RoomResult(RoomError.None, await _rooms.FindByIdAsync(roomId, cancellationToken));
    }

    /// <summary>
    /// شروع بازی: از روی اعضای فعلی یک بازی ساخته و ذخیره می‌شود و اتاق به آن گره می‌خورد.
    /// ترتیب صندلی‌ها همان ترتیب نوبت است.
    /// </summary>
    public async Task<RoomResult> StartAsync(
        Guid roomId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var room = await _rooms.FindByIdAsync(roomId, cancellationToken);
        if (room is null)
        {
            return RoomResult.Fail(RoomError.RoomNotFound);
        }

        if (!room.IsHost(userId))
        {
            return RoomResult.Fail(RoomError.NotHost);
        }

        if (room.Status == RoomStatus.Started)
        {
            return RoomResult.Fail(RoomError.RoomAlreadyStarted);
        }

        if (room.Status == RoomStatus.Closed)
        {
            return RoomResult.Fail(RoomError.RoomClosed);
        }

        if (room.Members.Count < 2)
        {
            return RoomResult.Fail(RoomError.NotEnoughPlayers);
        }

        var seats = room.Members.OrderBy(m => m.Seat).Select(m => m.UserId).ToList();
        var seed = room.Settings.Seed ?? NewSeed();

        // برد سفارشی اندازه‌ی خودش را دارد؛ تنظیمات باید با آن بخواند وگرنه بازی
        // با شعاعی شروع می‌شود که با خانه‌های واقعی نمی‌سازد.
        Domain.Board.BoardLayout? layout = null;
        if (room.Settings.HasCustomBoard
            && !Domain.Board.BoardCode.TryDecode(room.Settings.BoardCode, out layout, out _))
        {
            return RoomResult.Fail(RoomError.InvalidSettings);
        }

        var options = room.Settings.ToGameOptions(seats.Count, seed) with
        {
            BoardRadius = layout?.Radius ?? room.Settings.BoardRadius
        };

        var gameId = await _games.CreateAsync(options, seats, layout, cancellationToken);
        await _rooms.AttachGameAsync(roomId, gameId, cancellationToken);

        return new RoomResult(RoomError.None, await _rooms.FindByIdAsync(roomId, cancellationToken), gameId);
    }

    private async Task<string> NewCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            var code = RoomCode.New();
            if (!await _rooms.CodeExistsAsync(code, cancellationToken))
            {
                return code;
            }
        }

        throw new InvalidOperationException("تولید کد یکتا برای اتاق ناموفق بود.");
    }

    private static ulong NewSeed()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return BitConverter.ToUInt64(bytes);
    }
}
