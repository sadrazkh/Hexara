using Hexara.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Hexara.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<AppUser, AppRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

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

        // جدول‌های Identity با پیشوند تا از جدول‌های بازی (فاز ۲) جدا بمانند.
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
