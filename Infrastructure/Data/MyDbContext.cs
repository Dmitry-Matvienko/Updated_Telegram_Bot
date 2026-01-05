using Microsoft.EntityFrameworkCore;
using MyUpdatedBot.Core.Models.Entities;

namespace MyUpdatedBot.Infrastructure.Data
{
    public class MyDbContext : DbContext
    {
        public DbSet<UserEntity> Users { get; set; } = default!;
        public DbSet<MessageCountEntity> MessageStats { get; set; } = default!;
        public DbSet<ReputationEntity> RatingStats { get; set; } = default!; // TODO: Rename to  ReputationStats and update DB
        public DbSet<ReputationGivenEntity> ReputationGivens { get; set; } = default!;
        public DbSet<WarningRecord> WarningRecords { get; set; } = default!;

        public MyDbContext(DbContextOptions<MyDbContext> opts) : base(opts) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Unique index on business key UserId
            modelBuilder.Entity<UserEntity>()
                .HasIndex(u => u.UserId)
                .IsUnique();

            // One-to-many communication setup: User —> MessageStats
            modelBuilder.Entity<MessageCountEntity>()
                .HasOne(ms => ms.User)
                .WithMany(u => u.MessageStats)
                .HasForeignKey(ms => ms.UserRefId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReputationEntity>()
              .HasKey(l => l.Id);
            modelBuilder.Entity<ReputationEntity>()
              .HasIndex(l => new { l.UserRefId, l.ChatId })
              .IsUnique();
            modelBuilder.Entity<ReputationEntity>()
              .HasOne(l => l.User)
              .WithMany(u => u.RatingStats)
              .HasForeignKey(l => l.UserRefId);
            modelBuilder.Entity<ReputationGivenEntity>()
              .HasIndex(g => new { g.FromUserId, g.ToUserRefId, g.ChatId })
              .IsUnique();

            modelBuilder.Entity<WarningRecord>(b =>
            {
                b.HasKey(w => w.Id);
                b.HasIndex(w => new { w.UserRefId, w.ChatId }).IsUnique();
                b.Property(w => w.CreatedAtUtc).IsRequired();
                b.Property(w => w.WarningsCount).HasDefaultValue(0);
                b.Property(w => w.RowVersion).IsRowVersion();
                b.HasOne(w => w.User)
                 .WithMany()
                 .HasForeignKey(w => w.UserRefId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
