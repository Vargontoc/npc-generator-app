using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Npc.Api.Entities;

namespace Npc.Api.Data;

public class CharacterDbContext : DbContext
{
    public CharacterDbContext(DbContextOptions<CharacterDbContext> opts) : base(opts) { }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Characters
        var character = modelBuilder.Entity<Character>();
        character.ToTable("characters");
        character.HasKey(c => c.Id);
        character.Property(c => c.Name).IsRequired().HasMaxLength(100);
        character.Property(c => c.Age).IsRequired();
        character.Property(c => c.Description).HasMaxLength(2000);
        character.Property(c => c.AvatarUrl);
        character.Property(c => c.CreatedAt).IsRequired();
        character.Property(c => c.UpdateAt).IsRequired();
        character.HasIndex(c => c.Name);
    }
    

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Character>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdateAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdateAt = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}