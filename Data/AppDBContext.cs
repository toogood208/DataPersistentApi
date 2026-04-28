using DataPersistentApi.Models;
using Microsoft.EntityFrameworkCore;

namespace DataPersistentApi.Data;

public class AppDBContext: DbContext
{
    public AppDBContext(DbContextOptions<AppDBContext> options) : base(options)
    {
    }

    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Profile>(eb =>
        {
            eb.HasKey(p => p.Id);
            eb.Property(p => p.Id).HasColumnType("varchar(36)");
            eb.Property(p => p.Name).IsRequired().HasColumnType("varchar(255)");
            eb.Property(p => p.Gender).IsRequired().HasColumnType("varchar(20)");
            eb.Property(p => p.AgeGroup).IsRequired().HasColumnType("varchar(20)");
            eb.Property(p => p.CountryId).IsRequired().HasColumnType("varchar(2)");
            eb.Property(p => p.CountryName).IsRequired().HasColumnType("varchar(100)");
            eb.Property(p => p.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            eb.HasIndex(p => p.Name).IsUnique();
            eb.HasIndex(p => p.Gender);
            eb.HasIndex(p => p.Age);
            eb.HasIndex(p => p.AgeGroup);
            eb.HasIndex(p => p.CountryId);
            eb.HasIndex(p => p.GenderProbability);
            eb.HasIndex(p => p.CountryProbability);
        });

        modelBuilder.Entity<User>(eb =>
        {
            eb.HasKey(u => u.Id);
            eb.Property(u => u.Id).HasColumnType("varchar(36)");
            eb.Property(u => u.GitHubId).IsRequired().HasColumnType("varchar(50)");
            eb.Property(u => u.Username).IsRequired().HasColumnType("varchar(255)");
            eb.Property(u => u.Email).HasColumnType("varchar(320)");
            eb.Property(u => u.AvatarUrl).HasColumnType("varchar(500)");
            eb.Property(u => u.Role).IsRequired().HasColumnType("varchar(20)").HasDefaultValue("analyst");
            eb.Property(u => u.IsActive).HasDefaultValue(true);
            eb.Property(u => u.LastLoginAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            eb.Property(u => u.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            eb.HasIndex(u => u.GitHubId).IsUnique();
            eb.HasIndex(u => u.Username).IsUnique();
            eb.HasIndex(u => u.Email);
            eb.HasIndex(u => u.Role);
            eb.HasIndex(u => u.IsActive);
        });

        modelBuilder.Entity<RefreshToken>(eb =>
        {
            eb.HasKey(rt => rt.Id);
            eb.Property(rt => rt.Id).HasColumnType("varchar(36)");
            eb.Property(rt => rt.UserId).IsRequired().HasColumnType("varchar(36)");
            eb.Property(rt => rt.TokenHash).IsRequired().HasColumnType("varchar(200)");
            eb.Property(rt => rt.ExpiresAt).HasColumnType("datetime2");
            eb.Property(rt => rt.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            eb.Property(rt => rt.RevokedAt).HasColumnType("datetime2");
            eb.Property(rt => rt.ReplacedByTokenHash).HasColumnType("varchar(200)");
            eb.HasIndex(rt => rt.TokenHash).IsUnique();
            eb.HasIndex(rt => rt.UserId);
            eb.HasIndex(rt => rt.ExpiresAt);
            eb.HasIndex(rt => rt.RevokedAt);
            eb.HasOne(rt => rt.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

}
