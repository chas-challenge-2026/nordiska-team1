using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Agreements.Domain;
using Nordiska.Modules.Communication.Domain;
using Nordiska.Modules.CustomerCenter.Domain;
using InboxDocument = Nordiska.Modules.Documents.Domain.Document;
using CustomerDocument = Nordiska.Modules.Documents.Domain.CustomerDocument;
using InboxMessage = Nordiska.Modules.Communication.Domain.Message;

namespace Nordiska.Modules.Inbox.Infrastructure.Db;

public sealed class InboxDbContext(
    DbContextOptions<InboxDbContext> options)
    : DbContext(options)
{
    public DbSet<Term> Terms => Set<Term>();
    public DbSet<TermAcceptance> TermAcceptances => Set<TermAcceptance>();

    public DbSet<MessageBox> MessageBoxes => Set<MessageBox>();
    public DbSet<MessageThread> MessageThreads => Set<MessageThread>();
    public DbSet<MessageThreadState> MessageThreadStates =>
        Set<MessageThreadState>();
    public DbSet<InboxMessage> Messages => Set<InboxMessage>();

    public DbSet<CustomerNotification> CustomerNotifications =>
        Set<CustomerNotification>();
    public DbSet<FeedItem> FeedItems => Set<FeedItem>();

    public DbSet<InboxDocument> Documents => Set<InboxDocument>();
    public DbSet<CustomerDocument> CustomerDocuments =>
        Set<CustomerDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(InboxDatabase.Details.Schema);

        ConfigureDocuments(modelBuilder);
        ConfigureAgreements(modelBuilder);
        ConfigureMessages(modelBuilder);
        ConfigureNotifications(modelBuilder);
        ConfigureFeed(modelBuilder);
    }

    private static void ConfigureDocuments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InboxDocument>(builder =>
        {
            builder.ToTable("documents");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.Property(x => x.DocumentType)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.Title)
                .HasMaxLength(300)
                .IsRequired();

            builder.Property(x => x.FileName)
                .HasMaxLength(255)
                .IsRequired();

            builder.Property(x => x.MimeType)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.StorageKey)
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(x => x.Sha256)
                .HasMaxLength(64)
                .IsRequired();

            builder.Property(x => x.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(x => x.SourceType)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.SourceId)
                .HasMaxLength(100);

            builder.Property(x => x.CreatedAt).IsRequired();

            builder.HasIndex(x => x.StorageKey).IsUnique();
            builder.HasIndex(x => x.Sha256);
            builder.HasIndex(x => new { x.SourceType, x.SourceId });
        });

        modelBuilder.Entity<CustomerDocument>(builder =>
        {
            builder.ToTable("customer_documents");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.Property(x => x.DocumentId).IsRequired();
            builder.Property(x => x.CustomerId).IsRequired();
            builder.Property(x => x.PublishedAt).IsRequired();

            builder.HasIndex(x => new { x.CustomerId, x.DocumentId })
                .IsUnique();

            builder.HasIndex(x => new { x.CustomerId, x.PublishedAt });

            builder.HasOne<InboxDocument>()
                .WithMany()
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureAgreements(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Term>(builder =>
        {
            builder.ToTable("terms");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.Property(x => x.Code)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.Version).IsRequired();

            builder.Property(x => x.Title)
                .HasMaxLength(300)
                .IsRequired();

            builder.Property(x => x.DocumentId).IsRequired();

            builder.Property(x => x.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(x => x.EffectiveFrom).IsRequired();
            builder.Property(x => x.PublishedAt).IsRequired();

            builder.HasIndex(x => new { x.Code, x.Version })
                .IsUnique();

            builder.HasOne<InboxDocument>()
                .WithMany()
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TermAcceptance>(builder =>
        {
            builder.ToTable("term_acceptances");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.Property(x => x.TermId).IsRequired();
            builder.Property(x => x.CustomerId).IsRequired();

            builder.Property(x => x.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(x => x.CreatedAt).IsRequired();

            builder.HasIndex(x => new { x.TermId, x.CustomerId })
                .IsUnique();

            builder.HasIndex(x => new { x.CustomerId, x.Status });

            builder.HasOne<Term>()
                .WithMany()
                .HasForeignKey(x => x.TermId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureMessages(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MessageBox>(builder =>
        {
            builder.ToTable("message_boxes");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.Property(x => x.CustomerId).IsRequired();

            builder.Property(x => x.Type)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(x => x.CreatedAt).IsRequired();

            builder.HasIndex(x => x.CustomerId).IsUnique();
        });

        modelBuilder.Entity<MessageThread>(builder =>
        {
            builder.ToTable("message_threads");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.Property(x => x.MessageBoxId).IsRequired();

            builder.Property(x => x.Subject)
                .HasMaxLength(300)
                .IsRequired();

            builder.Property(x => x.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(x => x.CreatedAt).IsRequired();
            builder.Property(x => x.LastMessageAt).IsRequired();

            builder.HasIndex(x => new
            {
                x.MessageBoxId,
                x.LastMessageAt
            });

            builder.HasOne<MessageBox>()
                .WithMany()
                .HasForeignKey(x => x.MessageBoxId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable("messages");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.Property(x => x.ThreadId).IsRequired();

            builder.Property(x => x.SenderType)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(x => x.Body)
                .HasMaxLength(10_000)
                .IsRequired();

            builder.Property(x => x.ReplyAllowed).IsRequired();
            builder.Property(x => x.SentAt).IsRequired();

            builder.HasIndex(x => new { x.ThreadId, x.SentAt });

            builder.HasOne<MessageThread>()
                .WithMany()
                .HasForeignKey(x => x.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MessageThreadState>(builder =>
        {
            builder.ToTable("message_thread_states");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.Property(x => x.ThreadId).IsRequired();
            builder.Property(x => x.CustomerId).IsRequired();

            builder.Property(x => x.Folder)
                .HasConversion<int>()
                .IsRequired();

            builder.HasIndex(x => new { x.ThreadId, x.CustomerId })
                .IsUnique();

            builder.HasIndex(x => new
            {
                x.CustomerId,
                x.Folder,
                x.ReadAt
            });

            builder.HasOne<MessageThread>()
                .WithMany()
                .HasForeignKey(x => x.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureNotifications(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerNotification>(builder =>
        {
            builder.ToTable("customer_notifications");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.Property(x => x.CustomerId).IsRequired();

            builder.Property(x => x.Type)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.Priority)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(x => x.Title)
                .HasMaxLength(300)
                .IsRequired();

            builder.Property(x => x.Body)
                .HasMaxLength(5_000);

            builder.Property(x => x.TargetType)
                .HasConversion<int?>();

            builder.Property(x => x.CreatedAt).IsRequired();

            builder.HasIndex(x => new
            {
                x.CustomerId,
                x.ReadAt,
                x.CreatedAt
            });

            builder.HasIndex(x => new
            {
                x.TargetType,
                x.TargetId
            });
        });

    }

    private static void ConfigureFeed(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FeedItem>(builder =>
        {
            builder.ToTable("feed_items");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.Property(x => x.CustomerId).IsRequired();

            builder.Property(x => x.ItemType)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(x => x.SourceId).IsRequired();

            builder.Property(x => x.Title)
                .HasMaxLength(300)
                .IsRequired();

            builder.Property(x => x.Preview)
                .HasMaxLength(1_000);

            builder.Property(x => x.Priority)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(x => x.ActionRequired).IsRequired();
            builder.Property(x => x.OccurredAt).IsRequired();

            builder.HasIndex(x => new
            {
                x.CustomerId,
                x.OccurredAt
            });

            builder.HasIndex(x => new
            {
                x.ItemType,
                x.SourceId,
                x.CustomerId
            }).IsUnique();
        });
    }

}
