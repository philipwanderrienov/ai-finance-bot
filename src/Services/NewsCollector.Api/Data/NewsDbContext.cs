using Microsoft.EntityFrameworkCore;
using NewsCollector.Api.Models;

namespace NewsCollector.Api.Data;

public sealed class NewsDbContext : DbContext
{
    public NewsDbContext(DbContextOptions<NewsDbContext> options)
        : base(options)
    {
    }

    public DbSet<NewsSourceEntity> NewsSources => Set<NewsSourceEntity>();
    public DbSet<NewsArticleEntity> NewsArticles => Set<NewsArticleEntity>();
    public DbSet<NewsSignalEntity> NewsSignals => Set<NewsSignalEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<NewsCategory>("news_category");
        modelBuilder.HasPostgresEnum<SignalAction>("signal_action");

        modelBuilder.Entity<NewsSourceEntity>(entity =>
        {
            entity.ToTable("news_sources");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
            entity.Property(x => x.BaseUrl).HasColumnName("base_url");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<NewsArticleEntity>(entity =>
        {
            entity.ToTable("news_articles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();
            entity.Property(x => x.SourceId).HasColumnName("source_id");
            entity.Property(x => x.SourceExternalId).HasColumnName("source_external_id");
            entity.Property(x => x.SourceName).HasColumnName("source_name").IsRequired();
            entity.Property(x => x.Title).HasColumnName("title").IsRequired();
            entity.Property(x => x.Summary).HasColumnName("summary").IsRequired();
            entity.Property(x => x.Url).HasColumnName("url").IsRequired();
            entity.Property(x => x.PublishedAt).HasColumnName("published_at");
            entity.Property(x => x.Category).HasColumnName("category").HasColumnType("news_category");
            entity.Property(x => x.SentimentScore).HasColumnName("sentiment_score");
            entity.Property(x => x.Keywords).HasColumnName("keywords");
            entity.Property(x => x.RawPayload).HasColumnName("raw_payload").HasColumnType("jsonb");
            entity.Property(x => x.IngestedAt).HasColumnName("ingested_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(x => x.Url).IsUnique();
            entity.HasIndex(x => new { x.SourceName, x.SourceExternalId }).IsUnique();
        });

        modelBuilder.Entity<NewsSignalEntity>(entity =>
        {
            entity.ToTable("news_signals");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();
            entity.Property(x => x.NewsCategory).HasColumnName("news_category").HasColumnType("news_category");
            entity.Property(x => x.Symbol).HasColumnName("symbol").IsRequired();
            entity.Property(x => x.Action).HasColumnName("action").HasColumnType("signal_action");
            entity.Property(x => x.Confidence).HasColumnName("confidence");
            entity.Property(x => x.SuggestedPrice).HasColumnName("suggested_price");
            entity.Property(x => x.Reason).HasColumnName("reason").IsRequired();
            entity.Property(x => x.GeneratedAt).HasColumnName("generated_at");
        });

        base.OnModelCreating(modelBuilder);
    }
}

public sealed class NewsSourceEntity
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class NewsArticleEntity
{
    public Guid Id { get; set; }
    public long SourceId { get; set; }
    public string? SourceExternalId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; set; }
    public NewsCategory Category { get; set; }
    public decimal? SentimentScore { get; set; }
    public List<string>? Keywords { get; set; }
    public string? RawPayload { get; set; }
    public DateTimeOffset IngestedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class NewsSignalEntity
{
    public Guid Id { get; set; }
    public NewsCategory NewsCategory { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public SignalAction Action { get; set; }
    public decimal Confidence { get; set; }
    public decimal SuggestedPrice { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
}

public enum SignalAction
{
    Buy = 0,
    Sell = 1,
    Hold = 2
}
