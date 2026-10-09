using Microsoft.EntityFrameworkCore;

namespace Anime.Infrastructure.Persistence;

public sealed class AnimeDbContext(DbContextOptions<AnimeDbContext> options) : DbContext(options)
{
    public DbSet<CachedResponse> CachedResponses => Set<CachedResponse>();

    public DbSet<CatalogAnime> Anime => Set<CatalogAnime>();

    public DbSet<CatalogListItem> ListItems => Set<CatalogListItem>();

    public DbSet<CatalogList> Lists => Set<CatalogList>();

    public DbSet<SyncJob> SyncJobs => Set<SyncJob>();

    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    public DbSet<AnimeProviderStatus> ProviderStatuses => Set<AnimeProviderStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CachedResponse>(entity =>
        {
            entity.ToTable("cached_responses");
            entity.HasKey(x => x.Key);

            entity.Property(x => x.Key).HasColumnName("key");
            entity.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(32);
            entity.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
            entity.Property(x => x.FetchedAt).HasColumnName("fetched_at");
            entity.Property(x => x.ExpiresAt).HasColumnName("expires_at");

            entity.HasIndex(x => x.Kind);
            entity.HasIndex(x => x.ExpiresAt);
        });

        modelBuilder.Entity<CatalogAnime>(entity =>
        {
            entity.ToTable("anime");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.DisplayTitle).HasColumnName("display_title").HasMaxLength(500);
            entity.Property(x => x.SearchText).HasColumnName("search_text");
            entity.Property(x => x.TitlesJson).HasColumnName("titles").HasColumnType("jsonb");
            entity.Property(x => x.ImagesJson).HasColumnName("images").HasColumnType("jsonb");
            entity.Property(x => x.Format).HasColumnName("format").HasMaxLength(32);
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            entity.Property(x => x.Season).HasColumnName("season").HasMaxLength(16);
            entity.Property(x => x.SeasonYear).HasColumnName("season_year");
            entity.Property(x => x.StartDate).HasColumnName("start_date").HasMaxLength(16);
            entity.Property(x => x.TotalUnits).HasColumnName("total_units");
            entity.Property(x => x.Providers).HasColumnName("providers");
            entity.Property(x => x.HasStream).HasColumnName("has_stream");
            entity.Property(x => x.IsHidden).HasColumnName("is_hidden");
            entity.Property(x => x.AutoHidden).HasColumnName("auto_hidden");
            entity.Property(x => x.SyncedAt).HasColumnName("synced_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(x => new { x.HasStream, x.IsHidden });
            entity.HasIndex(x => x.DisplayTitle);
        });

        modelBuilder.Entity<CatalogListItem>(entity =>
        {
            entity.ToTable("anime_list_items");
            entity.HasKey(x => new { x.ListKey, x.Position });

            entity.Property(x => x.ListKey).HasColumnName("list_key").HasMaxLength(64);
            entity.Property(x => x.Position).HasColumnName("position");
            entity.Property(x => x.AnimeId).HasColumnName("anime_id");

            entity.HasIndex(x => x.AnimeId);
            entity.HasOne<CatalogAnime>().WithMany().HasForeignKey(x => x.AnimeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CatalogList>(entity =>
        {
            entity.ToTable("anime_lists");
            entity.HasKey(x => x.Key);

            entity.Property(x => x.Key).HasColumnName("key").HasMaxLength(64);
            entity.Property(x => x.ItemCount).HasColumnName("item_count");
            entity.Property(x => x.SyncedAt).HasColumnName("synced_at");
        });

        modelBuilder.Entity<SyncJob>(entity =>
        {
            entity.ToTable("sync_jobs");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16);
            entity.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(16).HasDefaultValue(SyncJobKind.Catalog);
            entity.Property(x => x.AnimeId).HasColumnName("anime_id");
            entity.Property(x => x.Target).HasColumnName("target").HasMaxLength(500);
            entity.Property(x => x.Lists).HasColumnName("lists");
            entity.Property(x => x.Progress).HasColumnName("progress").HasMaxLength(500);
            entity.Property(x => x.Processed).HasColumnName("processed");
            entity.Property(x => x.Added).HasColumnName("added");
            entity.Property(x => x.Updated).HasColumnName("updated");
            entity.Property(x => x.Skipped).HasColumnName("skipped");
            entity.Property(x => x.UpdateExisting).HasColumnName("update_existing");
            entity.Property(x => x.Error).HasColumnName("error");
            entity.Property(x => x.TriggeredBy).HasColumnName("triggered_by").HasMaxLength(64);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.StartedAt).HasColumnName("started_at");
            entity.Property(x => x.FinishedAt).HasColumnName("finished_at");

            entity.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<AnimeProviderStatus>(entity =>
        {
            entity.ToTable("anime_provider_status");
            entity.HasKey(x => new { x.AnimeId, x.Provider });

            entity.Property(x => x.AnimeId).HasColumnName("anime_id");
            entity.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(32);
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16);
            entity.Property(x => x.IsDisabled).HasColumnName("is_disabled");
            entity.Property(x => x.DisabledBy).HasColumnName("disabled_by").HasMaxLength(16);
            entity.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(500);
            entity.Property(x => x.CheckedAt).HasColumnName("checked_at");

            entity.HasIndex(x => x.Status);
            entity.HasOne<CatalogAnime>().WithMany().HasForeignKey(x => x.AnimeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.ToTable("admin_users");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Username).HasColumnName("username").HasMaxLength(64);
            entity.Property(x => x.PasswordHash).HasColumnName("password_hash");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.LastLoginAt).HasColumnName("last_login_at");

            entity.HasIndex(x => x.Username).IsUnique();
        });
    }
}
