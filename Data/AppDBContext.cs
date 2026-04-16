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
            eb.Property(p => p.CreatedAt).HasColumnType("datetime2");
            eb.Property(p => p.Name).IsRequired();
            eb.Property(p => p.CountryProbability).HasColumnType("float");
        });
    }

}
