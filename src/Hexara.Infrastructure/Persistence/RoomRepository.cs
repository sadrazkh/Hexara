using Hexara.Application.Common.Interfaces;
using Hexara.Application.Rooms;
using Hexara.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hexara.Infrastructure.Persistence;

public sealed class RoomRepository : IRoomRepository
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public RoomRepository(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Room?> FindByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        Map(await Query().FirstOrDefaultAsync(r => r.Code == code, cancellationToken));

    public async Task<Room?> FindByIdAsync(Guid roomId, CancellationToken cancellationToken = default) =>
        Map(await Query().FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken));

    public async Task<Room> CreateAsync(
        Guid hostId,
        string code,
        RoomSettings settings,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow.UtcDateTime;
        var record = new RoomRecord
        {
            Id = Guid.NewGuid(),
            Code = code,
            HostId = hostId,
            Status = RoomStatus.Open,
            MaxPlayers = settings.MaxPlayers,
            VictoryPoints = settings.VictoryPoints,
            BoardRadius = settings.BoardRadius,
            FriendlyRobber = settings.FriendlyRobber,
            Seed = settings.Seed is { } seed ? unchecked((long)seed) : null,
            BoardCode = settings.BoardCode,
            CreatedAt = now,

            // میزبان خودش اولین کسی است که می‌نشیند.
            Members = [new RoomMemberRecord { UserId = hostId, Seat = 0, JoinedAt = now }]
        };

        _db.Rooms.Add(record);
        await _db.SaveChangesAsync(cancellationToken);

        return (await FindByIdAsync(record.Id, cancellationToken))!;
    }

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default) =>
        _db.Rooms.AnyAsync(r => r.Code == code, cancellationToken);

    public async Task<bool> AddMemberAsync(
        Guid roomId,
        Guid userId,
        int seat,
        CancellationToken cancellationToken = default)
    {
        _db.RoomMembers.Add(new RoomMemberRecord
        {
            RoomId = roomId,
            UserId = userId,
            Seat = seat,
            JoinedAt = _clock.UtcNow.UtcDateTime
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // یکتایی ‎(RoomId, Seat)‎ یا ‎(RoomId, UserId)‎ شکسته: یک نفر زودتر نشسته است.
            _db.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task RemoveMemberAsync(Guid roomId, Guid userId, CancellationToken cancellationToken = default)
    {
        await _db.RoomMembers
            .Where(m => m.RoomId == roomId && m.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task UpdateSettingsAsync(
        Guid roomId,
        RoomSettings settings,
        CancellationToken cancellationToken = default)
    {
        await _db.Rooms
            .Where(r => r.Id == roomId)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(r => r.MaxPlayers, settings.MaxPlayers)
                    .SetProperty(r => r.VictoryPoints, settings.VictoryPoints)
                    .SetProperty(r => r.BoardRadius, settings.BoardRadius)
                    .SetProperty(r => r.FriendlyRobber, settings.FriendlyRobber)
                    .SetProperty(r => r.Seed, settings.Seed is { } seed ? unchecked((long)seed) : null)
                    .SetProperty(r => r.BoardCode, settings.BoardCode),
                cancellationToken);
    }

    public async Task SetHostAsync(Guid roomId, Guid hostId, CancellationToken cancellationToken = default)
    {
        await _db.Rooms
            .Where(r => r.Id == roomId)
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.HostId, hostId), cancellationToken);
    }

    public async Task SetStatusAsync(Guid roomId, RoomStatus status, CancellationToken cancellationToken = default)
    {
        await _db.Rooms
            .Where(r => r.Id == roomId)
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.Status, status), cancellationToken);
    }

    public async Task AttachGameAsync(Guid roomId, Guid gameId, CancellationToken cancellationToken = default)
    {
        await _db.Rooms
            .Where(r => r.Id == roomId)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(r => r.GameId, gameId)
                    .SetProperty(r => r.Status, RoomStatus.Started),
                cancellationToken);
    }

    public async Task<IReadOnlyList<RoomSummary>> ListOpenAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.Rooms
            .AsNoTracking()
            .Where(r => r.Status == RoomStatus.Open)
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .Select(r => new
            {
                r.Id,
                r.Code,
                r.Status,
                HostName = r.Host!.DisplayName,
                MemberCount = r.Members.Count,
                r.MaxPlayers,
                r.VictoryPoints,
                r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new RoomSummary(
            r.Id, r.Code, r.Status, r.HostName, r.MemberCount, r.MaxPlayers, r.VictoryPoints, Utc(r.CreatedAt)))];
    }

    public async Task<IReadOnlyList<RoomSummary>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.Rooms
            .AsNoTracking()
            .Where(r => r.Status != RoomStatus.Closed && r.Members.Any(m => m.UserId == userId))
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Code,
                r.Status,
                HostName = r.Host!.DisplayName,
                MemberCount = r.Members.Count,
                r.MaxPlayers,
                r.VictoryPoints,
                r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new RoomSummary(
            r.Id, r.Code, r.Status, r.HostName, r.MemberCount, r.MaxPlayers, r.VictoryPoints, Utc(r.CreatedAt)))];
    }

    private IQueryable<RoomRecord> Query() =>
        _db.Rooms
            .AsNoTracking()
            .Include(r => r.Members)
            .ThenInclude(m => m.User);

    private static Room? Map(RoomRecord? record)
    {
        if (record is null)
        {
            return null;
        }

        var settings = new RoomSettings
        {
            MaxPlayers = record.MaxPlayers,
            VictoryPoints = record.VictoryPoints,
            BoardRadius = record.BoardRadius,
            FriendlyRobber = record.FriendlyRobber,
            Seed = record.Seed is { } seed ? unchecked((ulong)seed) : null,
            BoardCode = record.BoardCode
        };

        var members = record.Members
            .OrderBy(m => m.Seat)
            .Select(m => new RoomMember(
                m.Seat,
                m.UserId,
                m.User?.DisplayName ?? string.Empty,
                m.User?.AvatarColor ?? "#4f9cf9",
                m.User?.IsGuest ?? false))
            .ToList();

        return new Room(
            record.Id,
            record.Code,
            record.HostId,
            record.Status,
            settings,
            members,
            record.GameId,
            Utc(record.CreatedAt));
    }

    private static DateTimeOffset Utc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
