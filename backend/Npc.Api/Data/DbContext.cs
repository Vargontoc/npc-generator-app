using Microsoft.EntityFrameworkCore;
using Npc.Api.Entities;

namespace Npc.Api.Data;

public class CharacterDbContext : DbContext
{
    public CharacterDbContext(DbContextOptions<CharacterDbContext> opts) : base(opts) { }

    public DbSet<Character> Characters { get; set; }
    public DbSet<World> Worlds { get; set; }
    public DbSet<Lore> LoreEntries { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("public");

        // Characters
        var character = modelBuilder.Entity<Character>();
        character.ToTable("characters");
        character.HasKey(c => c.Id);
        character.Property(c => c.Name).IsRequired().HasMaxLength(100);
        character.Property(c => c.Age).IsRequired();
        character.Property(c => c.Description).HasMaxLength(2000);
        character.Property(c => c.AvatarUrl);
        character.Property(c => c.CreatedAt).IsRequired();
        character.Property(c => c.UpdatedAt).IsRequired();
        character.HasIndex(c => c.Name);

        // World
        var world = modelBuilder.Entity<World>();
        world.ToTable("worlds");
        world.HasKey(w => w.Id);
        world.Property(w => w.Name).IsRequired().HasMaxLength(120);
        world.Property(w => w.Description).HasMaxLength(4000);
        world.Property(w => w.CreatedAt).IsRequired();
        world.Property(w => w.UpdatedAt).IsRequired();

        // Lore
        var lore = modelBuilder.Entity<Lore>();
        lore.ToTable("lores");
        lore.HasKey(l => l.Id);
        lore.Property(l => l.Title).IsRequired().HasMaxLength(200);
        lore.Property(l => l.Text);
        lore.Property(l => l.CreatedAt).IsRequired();
        lore.Property(l => l.UpdatedAt).IsRequired();
        lore.HasOne(l => l.World).WithMany(w => w.LoreEntries).HasForeignKey(l => l.WorldId).OnDelete(DeleteBehavior.SetNull);
    }


    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is BaseEntity)
            {
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    entry.CurrentValues["UpdatedAt"] = now;
                    if (entry.State == EntityState.Added)
                    {
                        entry.CurrentValues["CreatedAt"] = now;
                    }
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
    

}