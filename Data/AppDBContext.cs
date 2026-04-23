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
            eb.Property(p => p.Name).IsRequired();
            eb.HasIndex(p => p.Name).IsUnique();
            eb.HasIndex(p => p.Gender);
            eb.HasIndex(p => p.Age);
            eb.HasIndex(p => p.AgeGroup);
            eb.HasIndex(p => p.CountryId);
            eb.HasIndex(p => p.GenderProbability);
            eb.HasIndex(p => p.CountryProbability);
            eb.Property(p => p.CreatedAt).HasColumnType("datetime2");
        });
    }

}
