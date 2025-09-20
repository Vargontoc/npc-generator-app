using Microsoft.EntityFrameworkCore;
using Npc.Api.Entities;

namespace Npc.Api.Data;

public class CharacterDbContext : DbContext
{
    public CharacterDbContext(DbContextOptions<CharacterDbContext> opts) : base(opts) { }

    public DbSet<Character> Characters { get; set; }
    public DbSet<World> Worlds { get; set; }
    public DbSet<Lore> LoreEntries { get; set; }
    public DbSet<Language> Languages { get; set; }
    public DbSet<LocalizedContent> LocalizedContents { get; set; }
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<Utterance> Utterances { get; set; }


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

        // Language
        var language = modelBuilder.Entity<Language>();
        language.ToTable("languages");
        language.HasKey(l => l.Id);
        language.Property(l => l.Code).IsRequired().HasMaxLength(10);
        language.Property(l => l.Name).IsRequired().HasMaxLength(100);
        language.Property(l => l.NativeName).IsRequired().HasMaxLength(100);
        language.Property(l => l.CultureInfo).HasMaxLength(20);
        language.Property(l => l.CreatedAt).IsRequired();
        language.Property(l => l.UpdatedAt).IsRequired();
        language.HasIndex(l => l.Code).IsUnique();

        // LocalizedContent
        var localizedContent = modelBuilder.Entity<LocalizedContent>();
        localizedContent.ToTable("localized_contents");
        localizedContent.HasKey(lc => lc.Id);
        localizedContent.Property(lc => lc.EntityType).IsRequired().HasMaxLength(50);
        localizedContent.Property(lc => lc.EntityId).IsRequired();
        localizedContent.Property(lc => lc.PropertyName).IsRequired().HasMaxLength(100);
        localizedContent.Property(lc => lc.LanguageCode).IsRequired().HasMaxLength(10);
        localizedContent.Property(lc => lc.Content).IsRequired();
        localizedContent.Property(lc => lc.Notes).HasMaxLength(500);
        localizedContent.Property(lc => lc.TranslatedBy).HasMaxLength(100);
        localizedContent.Property(lc => lc.CreatedAt).IsRequired();
        localizedContent.Property(lc => lc.UpdatedAt).IsRequired();
        localizedContent.HasIndex(lc => new { lc.EntityType, lc.EntityId, lc.PropertyName, lc.LanguageCode }).IsUnique();

        // Conversation
        var conversation = modelBuilder.Entity<Conversation>();
        conversation.ToTable("conversations");
        conversation.HasKey(c => c.Id);
        conversation.Property(c => c.Title).IsRequired().HasMaxLength(200);
        conversation.Property(c => c.CreatedAt).IsRequired();
        conversation.Property(c => c.UpdatedAt).IsRequired();
        conversation.HasOne(c => c.World).WithMany().HasForeignKey(c => c.WorldId).OnDelete(DeleteBehavior.SetNull);

        // Utterance
        var utterance = modelBuilder.Entity<Utterance>();
        utterance.ToTable("utterances");
        utterance.HasKey(u => u.Id);
        utterance.Property(u => u.Text).IsRequired();
        utterance.Property(u => u.ConversationId).IsRequired();
        utterance.Property(u => u.Version).IsRequired();
        utterance.Property(u => u.Deleted).IsRequired();
        utterance.Property(u => u.Tags).HasConversion(
            v => string.Join(',', v),
            v => v.Split(',', StringSplitOptions.RemoveEmptyEntries)
        );
        utterance.Property(u => u.CreatedAt).IsRequired();
        utterance.Property(u => u.UpdatedAt).IsRequired();
        utterance.HasOne(u => u.Conversation).WithMany(c => c.Utterances).HasForeignKey(u => u.ConversationId).OnDelete(DeleteBehavior.Cascade);
        utterance.HasOne(u => u.Character).WithMany().HasForeignKey(u => u.CharacterId).OnDelete(DeleteBehavior.SetNull);
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