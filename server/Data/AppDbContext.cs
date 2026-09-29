using Huamishu.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Huamishu.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RawEntry> RawEntries => Set<RawEntry>();
    public DbSet<Entry> Entries => Set<Entry>();
    public DbSet<ConfirmQa> ConfirmQas => Set<ConfirmQa>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<EntryTag> EntryTags => Set<EntryTag>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // 用户
        b.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Phone).IsUnique();
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(100).IsRequired();
            e.Property(x => x.Nickname).HasMaxLength(50);
            e.Property(x => x.Plan).HasMaxLength(20).HasDefaultValue("free");
        });

        // 原始记录（永不修改）
        b.Entity<RawEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
            e.Property(x => x.RawText).HasColumnType("TEXT").IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("pending");
        });

        // 分析结果
        b.Entity<Entry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.OccurredAt });
            e.HasIndex(x => new { x.UserId, x.Type });
            e.HasIndex(x => x.RawEntryId);
            e.HasOne<RawEntry>(x => x.RawEntry).WithMany().HasForeignKey(x => x.RawEntryId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
            e.Property(x => x.Type).HasMaxLength(20).IsRequired();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Amount).HasPrecision(14, 2);
            e.Property(x => x.Category).HasMaxLength(50);
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("confirmed");
            e.Property(x => x.Summary).HasColumnType("TEXT");
        });

        // 追问记录
        b.Entity<ConfirmQa>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.EntryId);
            e.Property(x => x.Question).HasColumnType("TEXT");
            e.Property(x => x.Answer).HasColumnType("TEXT");
            e.HasOne<Entry>().WithMany().HasForeignKey(x => x.EntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 标签（按用户隔离）
        b.Entity<Tag>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.Name }).IsUnique();
            e.HasOne<User>().WithMany(u => u.Tags).HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
        });

        // 记录-标签关联（多对多）
        b.Entity<EntryTag>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne<Entry>().WithMany(en => en.EntryTags).HasForeignKey(x => x.EntryId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Tag>().WithMany(t => t.EntryTags).HasForeignKey(x => x.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}