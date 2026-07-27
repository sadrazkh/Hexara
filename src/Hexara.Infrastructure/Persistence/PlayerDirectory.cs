using Hexara.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Hexara.Infrastructure.Persistence;

public sealed class PlayerDirectory : IPlayerDirectory
{
    private readonly AppDbContext _db;

    public PlayerDirectory(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PlayerProfile>> GetAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var ids = userIds.Distinct().ToList();

        return await _db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new PlayerProfile(u.Id, u.DisplayName, u.AvatarColor, u.IsGuest))
            .ToListAsync(cancellationToken);
    }
}
