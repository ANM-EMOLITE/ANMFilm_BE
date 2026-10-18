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

    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<WatchlistItem> Watchlist => Set<WatchlistItem>();

    public DbSet<WatchHistoryEntry> WatchHistory => Set<WatchHistoryEntry>();

    public DbSet<UserActivityLog> ActivityLogs => Set<UserActivityLog>();

    public DbSet<Comment> Comments => Set<Comment>();

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

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Username).HasColumnName("username").HasMaxLength(64);
            entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(256);
            entity.Property(x => x.PasswordHash).HasColumnName("password_hash");
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(64);
            entity.Property(x => x.IsBanned).HasColumnName("is_banned");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.LastLoginAt).HasColumnName("last_login_at");

            entity.HasIndex(x => x.Username).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<WatchlistItem>(entity =>
        {
            entity.ToTable("watchlist");
            entity.HasKey(x => new { x.UserId, x.AnimeId });

            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.AnimeId).HasColumnName("anime_id");
            entity.Property(x => x.Title).HasColumnName("title").HasMaxLength(500);
            entity.Property(x => x.Cover).HasColumnName("cover").HasMaxLength(1000);
            entity.Property(x => x.AddedAt).HasColumnName("added_at");

            entity.HasIndex(x => new { x.UserId, x.AddedAt });
            entity.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WatchHistoryEntry>(entity =>
        {
            entity.ToTable("watch_history");
            entity.HasKey(x => new { x.UserId, x.AnimeId });

            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.AnimeId).HasColumnName("anime_id");
            entity.Property(x => x.Title).HasColumnName("title").HasMaxLength(500);
            entity.Property(x => x.Cover).HasColumnName("cover").HasMaxLength(1000);
            entity.Property(x => x.Episode).HasColumnName("episode");
            entity.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(64);
            entity.Property(x => x.Count).HasColumnName("count");
            entity.Property(x => x.WatchedAt).HasColumnName("watched_at");

            entity.HasIndex(x => new { x.UserId, x.WatchedAt });
            entity.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserActivityLog>(entity =>
        {
            entity.ToTable("user_activity_logs");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Action).HasColumnName("action").HasMaxLength(32);
            entity.Property(x => x.TargetType).HasColumnName("target_type").HasMaxLength(32);
            entity.Property(x => x.TargetId).HasColumnName("target_id").HasMaxLength(64);
            entity.Property(x => x.Detail).HasColumnName("detail").HasMaxLength(1000);
            entity.Property(x => x.Ip).HasColumnName("ip").HasMaxLength(64);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
            entity.HasIndex(x => x.Action);
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.ToTable("comments");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.AnimeId).HasColumnName("anime_id");
            entity.Property(x => x.Episode).HasColumnName("episode");
            entity.Property(x => x.Content).HasColumnName("content").HasMaxLength(2000);
            entity.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.EditedAt).HasColumnName("edited_at");

            entity.HasIndex(x => new { x.AnimeId, x.CreatedAt });
            entity.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
