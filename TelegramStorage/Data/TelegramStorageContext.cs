using Microsoft.EntityFrameworkCore;
using TelegramStorage.Models;

namespace TelegramStorage.Data;

public class TelegramStorageContext : DbContext
{
    public TelegramStorageContext(DbContextOptions<TelegramStorageContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<FileRecord> FileRecords { get; set; }
    public DbSet<FileChunk> FileChunks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(255);
        });

        modelBuilder.Entity<FileRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TelegramFileId);
            entity.Property(e => e.OriginalFileName).HasMaxLength(500);
            entity.Property(e => e.ContentType).HasMaxLength(100);
            
            entity.HasOne(f => f.User)
                  .WithMany(u => u.Files)
                  .HasForeignKey(f => f.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FileChunk>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.FileRecordId, e.ChunkIndex }).IsUnique();
            entity.HasIndex(e => e.TelegramFileId);
            
            entity.HasOne(c => c.FileRecord)
                  .WithMany(f => f.Chunks)
                  .HasForeignKey(c => c.FileRecordId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}