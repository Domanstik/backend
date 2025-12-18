using Microsoft.EntityFrameworkCore;
using StarMarathon.Domain.Entities;

namespace StarMarathon.Infrastructure.Persistence;

public class StarDbContext : DbContext
{
    public StarDbContext(DbContextOptions<StarDbContext> options) : base(options) { }

    // Таблицы
    public DbSet<UserProfile> Profiles { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Contest> Contests { get; set; }
    public DbSet<ContestQuestion> ContestQuestions { get; set; }
    public DbSet<ContestAnswerOption> ContestAnswerOptions { get; set; }
    public DbSet<Document> Documents { get; set; }
    public DbSet<Purchase> Purchases { get; set; }
    public DbSet<ContestParticipant> ContestParticipants { get; set; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // 1. Называем таблицы в lowercase (стандарт Postgres/Supabase)
        mb.Entity<UserProfile>().ToTable("profiles");
        mb.Entity<Product>().ToTable("products");
        mb.Entity<Contest>().ToTable("contests");
        mb.Entity<ContestQuestion>().ToTable("contest_questions");
        mb.Entity<ContestAnswerOption>().ToTable("contest_answer_options");
        mb.Entity<Document>().ToTable("documents");
        mb.Entity<Purchase>().ToTable("purchases");
        mb.Entity<ContestParticipant>().ToTable("contest_participants");

        // 2. Настраиваем связи
        mb.Entity<ContestQuestion>()
            .HasOne(q => q.Contest)
            .WithMany(c => c.Questions)
            .HasForeignKey(q => q.ContestId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<ContestAnswerOption>()
            .HasOne(o => o.Question)
            .WithMany(q => q.Options)
            .HasForeignKey(o => o.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<Purchase>()
        .HasOne(p => p.User)
        .WithMany()
        .HasForeignKey(p => p.UserId)
        .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<Purchase>()
        .HasOne(p => p.Product)
        .WithMany()
        .HasForeignKey(p => p.ProductId)
        .OnDelete(DeleteBehavior.SetNull);

        mb.Entity<ContestParticipant>()
        .HasOne(cp => cp.Contest)
        .WithMany()
        .HasForeignKey(cp => cp.ContestId)
        .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<ContestParticipant>()
        .HasOne(cp => cp.User)
        .WithMany()
        .HasForeignKey(cp => cp.UserId)
        .OnDelete(DeleteBehavior.Cascade);

        // 3. Индексы для поиска по языку
        mb.Entity<Product>().HasIndex(p => p.Language);
        mb.Entity<Contest>().HasIndex(c => c.Language);
        mb.Entity<Document>().HasIndex(d => d.Language);
        mb.Entity<UserProfile>().HasIndex(u => u.LanguageCode);
    }
}