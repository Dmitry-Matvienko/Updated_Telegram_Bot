using Microsoft.EntityFrameworkCore;
using MyUpdatedBot.Core.Models.Entities;

namespace MyUpdatedBot.Infrastructure.Data
{
    public class MyDbContext : DbContext
    {
        public DbSet<UserEntity> Users { get; set; } = default!;
        public DbSet<MessageCountEntity> MessageStats { get; set; } = default!;

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
        }
    }
}
