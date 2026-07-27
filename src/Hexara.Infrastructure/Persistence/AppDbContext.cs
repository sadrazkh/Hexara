using Hexara.Infrastructure.Identity;
using Hexara.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Hexara.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<AppUser, AppRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<GameRecord> Games => Set<GameRecord>();

    public DbSet<GamePlayerRecord> GamePlayers => Set<GamePlayerRecord>();

    public DbSet<GameMoveRecord> GameMoves => Set<GameMoveRecord>();

    public DbSet<RoomRecord> Rooms => Set<RoomRecord>();

    public DbSet<RoomMemberRecord> RoomMembers => Set<RoomMemberRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(e =>
        {
            e.Property(u => u.DisplayName).HasMaxLength(60).IsRequired();
            e.Property(u => u.AvatarColor).HasMaxLength(9);
            e.Property(u => u.PreferredLanguage).HasMaxLength(8);
            e.HasIndex(u => u.IsGuest);
        });

        // ستون jsonb فقط در Postgres وجود دارد؛ تست‌ها روی SQLite اجرا می‌شوند.
        var json = Database.IsNpgsql() ? "jsonb" : null;

        builder.Entity<GameRecord>(e =>
        {
            e.ToTable("Games");
            e.HasKey(g => g.Id);
            e.Property(g => g.Snapshot).IsRequired();
            if (json is not null)
            {
                e.Property(g => g.Snapshot).HasColumnType(json);
            }

            // نسخه‌ی وضعیت، توکن هم‌زمانی است: دو حرکت هم‌زمان روی یک بازی، دومی رد می‌شود.
            e.Property(g => g.Version).IsConcurrencyToken();

            e.HasIndex(g => g.Status);
            e.HasIndex(g => g.UpdatedAt);
        });

        builder.Entity<GamePlayerRecord>(e =>
        {
            e.ToTable("GamePlayers");
            e.HasKey(p => new { p.GameId, p.Seat });
            e.HasIndex(p => p.UserId);
            e.HasIndex(p => new { p.GameId, p.UserId }).IsUnique();

            e.HasOne(p => p.Game)
                .WithMany(g => g.Players)
                .HasForeignKey(p => p.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<GameMoveRecord>(e =>
        {
            e.ToTable("GameMoves");
            e.HasKey(m => m.Id);
            e.HasIndex(m => new { m.GameId, m.Sequence }).IsUnique();
            e.Property(m => m.Action).IsRequired();
            e.Property(m => m.Events).IsRequired();
            if (json is not null)
            {
                e.Property(m => m.Action).HasColumnType(json);
                e.Property(m => m.Events).HasColumnType(json);
            }

            e.HasOne(m => m.Game)
                .WithMany(g => g.Moves)
                .HasForeignKey(m => m.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RoomRecord>(e =>
        {
            e.ToTable("Rooms");
            e.HasKey(r => r.Id);
            e.Property(r => r.Code).HasMaxLength(12).IsRequired();
            e.HasIndex(r => r.Code).IsUnique();
            e.HasIndex(r => new { r.Status, r.CreatedAt });

            e.HasOne(r => r.Host)
                .WithMany()
                .HasForeignKey(r => r.HostId)
                .OnDelete(DeleteBehavior.Cascade);

            // بازی بعد از شروع ساخته می‌شود؛ حذف بازی نباید اتاق را با خودش ببرد.
            e.HasOne(r => r.Game)
                .WithMany()
                .HasForeignKey(r => r.GameId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<RoomMemberRecord>(e =>
        {
            e.ToTable("RoomMembers");
            e.HasKey(m => new { m.RoomId, m.UserId });
            e.HasIndex(m => new { m.RoomId, m.Seat }).IsUnique();
            e.HasIndex(m => m.UserId);

            e.HasOne(m => m.Room)
                .WithMany(r => r.Members)
                .HasForeignKey(m => m.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // جدول‌های Identity با پیشوند تا از جدول‌های بازی جدا بمانند.
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is not null && table.StartsWith("AspNet", StringComparison.Ordinal))
            {
                entity.SetTableName(table.Replace("AspNet", "Identity", StringComparison.Ordinal));
            }
        }
    }
}
