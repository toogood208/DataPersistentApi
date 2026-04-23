using DataPersistentApi.Models;
using Microsoft.EntityFrameworkCore;

namespace DataPersistentApi.Data;

public class AppDBContext: DbContext
{
    public AppDBContext(DbContextOptions<AppDBContext> options) : base(options)
    {
    }

    public DbSet<Profile> Profiles => Set<Profile>();

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
    }

}
