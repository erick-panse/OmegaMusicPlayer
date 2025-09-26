using Microsoft.EntityFrameworkCore;
using OmegaMusicPlayer.Infrastructure.Data.Entities;
using System;

namespace OmegaMusicPlayer.Infrastructure.Data
{
    /// <summary>
    /// Entity Framework DbContext for OmegaMusicPlayer PostgreSQL database
    /// </summary>
    public class OmegaMusicPlayerDbContext : DbContext
    {
        public OmegaMusicPlayerDbContext(DbContextOptions<OmegaMusicPlayerDbContext> options) : base(options)
        {
        }

        // DbSets for all entities
        public DbSet<Media> Media { get; set; }
        public DbSet<Profile> Profiles { get; set; }
        public DbSet<Artist> Artists { get; set; }
        public DbSet<Album> Albums { get; set; }
        public DbSet<Track> Tracks { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<Playlist> Playlists { get; set; }
        public DbSet<PlaylistTrack> PlaylistTracks { get; set; }
        public DbSet<TrackArtist> TrackArtists { get; set; }
        public DbSet<TrackGenre> TrackGenres { get; set; }
        public DbSet<CurrentQueue> CurrentQueues { get; set; }
        public DbSet<QueueTrack> QueueTracks { get; set; }
        public DbSet<GlobalConfig> GlobalConfigs { get; set; }
        public DbSet<ProfileConfig> ProfileConfigs { get; set; }
        public DbSet<Entities.Directory> Directories { get; set; }
        public DbSet<PlayHistory> PlayHistories { get; set; }
        public DbSet<PlayCount> PlayCounts { get; set; }
        public DbSet<Like> Likes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure composite keys for junction tables
            modelBuilder.Entity<TrackArtist>()
                .HasKey(ta => new { ta.TrackId, ta.ArtistId });

            modelBuilder.Entity<TrackGenre>()
                .HasKey(tg => new { tg.TrackId, tg.GenreId });

            modelBuilder.Entity<PlaylistTrack>()
                .HasKey(pt => new { pt.PlaylistId, pt.TrackOrder });

            modelBuilder.Entity<QueueTrack>()
                .HasKey(qt => new { qt.QueueId, qt.TrackOrder });

            modelBuilder.Entity<PlayCount>()
                .HasKey(pc => new { pc.ProfileId, pc.TrackId });

            modelBuilder.Entity<Like>()
                .HasKey(l => new { l.ProfileId, l.TrackId });

            // Configure unique constraints
            modelBuilder.Entity<Artist>()
                .HasIndex(a => a.ArtistName)
                .IsUnique();

            modelBuilder.Entity<Profile>()
                .HasIndex(p => p.ProfileName)
                .IsUnique();

            modelBuilder.Entity<Genre>()
                .HasIndex(g => g.GenreName)
                .IsUnique();

            modelBuilder.Entity<Entities.Directory>()
                .HasIndex(d => d.DirPath)
                .IsUnique();

            modelBuilder.Entity<Playlist>()
                .HasIndex(p => new { p.Title, p.ProfileId })
                .IsUnique();

            modelBuilder.Entity<ProfileConfig>()
                .HasIndex(pc => pc.ProfileId)
                .IsUnique();

            // Configure indexes for performance
            modelBuilder.Entity<GlobalConfig>()
                .HasIndex(gc => gc.LastUsedProfile);

            modelBuilder.Entity<ProfileConfig>()
                .HasIndex(pc => pc.ProfileId);

            modelBuilder.Entity<PlayHistory>()
                .HasIndex(ph => ph.ProfileId);

            modelBuilder.Entity<PlayHistory>()
                .HasIndex(ph => new { ph.ProfileId, ph.PlayedAt });

            modelBuilder.Entity<PlayCount>()
                .HasIndex(pc => pc.ProfileId);

            modelBuilder.Entity<PlayCount>()
                .HasIndex(pc => pc.TrackId);

            modelBuilder.Entity<Like>()
                .HasIndex(l => l.ProfileId);

            modelBuilder.Entity<Like>()
                .HasIndex(l => l.TrackId);

            // Configure relationships and cascade behaviors
            ConfigureRelationships(modelBuilder);

            // Configure PostgreSQL-specific settings
            ConfigurePostgreSqlSettings(modelBuilder);
        }

        private void ConfigureRelationships(ModelBuilder modelBuilder)
        {
            // Profile relationships with cascade delete
            modelBuilder.Entity<Playlist>()
                .HasOne(p => p.Profile)
                .WithMany(pr => pr.Playlists)
                .HasForeignKey(p => p.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CurrentQueue>()
                .HasOne(cq => cq.Profile)
                .WithMany(p => p.CurrentQueues)
                .HasForeignKey(cq => cq.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlayHistory>()
                .HasOne(ph => ph.Profile)
                .WithMany(p => p.PlayHistories)
                .HasForeignKey(ph => ph.ProfileId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PlayCount>()
                .HasOne(pc => pc.Profile)
                .WithMany(p => p.PlayCounts)
                .HasForeignKey(pc => pc.ProfileId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Like>()
                .HasOne(l => l.Profile)
                .WithMany(p => p.Likes)
                .HasForeignKey(l => l.ProfileId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ProfileConfig>()
                .HasOne(pc => pc.Profile)
                .WithOne(p => p.ProfileConfig)
                .HasForeignKey<ProfileConfig>(pc => pc.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            // Track relationships with cascade delete
            modelBuilder.Entity<PlayHistory>()
                .HasOne(ph => ph.Track)
                .WithMany(t => t.PlayHistories)
                .HasForeignKey(ph => ph.TrackId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlayCount>()
                .HasOne(pc => pc.Track)
                .WithMany(t => t.PlayCounts)
                .HasForeignKey(pc => pc.TrackId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Like>()
                .HasOne(l => l.Track)
                .WithMany(t => t.Likes)
                .HasForeignKey(l => l.TrackId)
                .OnDelete(DeleteBehavior.Cascade);

            // Queue relationships
            modelBuilder.Entity<QueueTrack>()
                .HasOne(qt => qt.CurrentQueue)
                .WithMany(cq => cq.QueueTracks)
                .HasForeignKey(qt => qt.QueueId)
                .OnDelete(DeleteBehavior.Cascade);

            // Global config relationship
            modelBuilder.Entity<GlobalConfig>()
                .HasOne(gc => gc.LastUsedProfileNavigation)
                .WithMany()
                .HasForeignKey(gc => gc.LastUsedProfile)
                .OnDelete(DeleteBehavior.SetNull);

            // Media relationships with set null on delete
            modelBuilder.Entity<Profile>()
                .HasOne(p => p.Photo)
                .WithMany(m => m.ProfilesWithPhoto)
                .HasForeignKey(p => p.PhotoId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Track>()
                .HasOne(t => t.Cover)
                .WithMany(m => m.TracksWithCover)
                .HasForeignKey(t => t.CoverId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Album>()
                .HasOne(a => a.Cover)
                .WithMany(m => m.AlbumsWithCover)
                .HasForeignKey(a => a.CoverId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Artist>()
                .HasOne(a => a.Photo)
                .WithMany(m => m.ArtistsWithPhoto)
                .HasForeignKey(a => a.PhotoId)
                .OnDelete(DeleteBehavior.SetNull);
        }

        private void ConfigurePostgreSqlSettings(ModelBuilder modelBuilder)
        {
            // Configure string length constraints and PostgreSQL-specific settings
            modelBuilder.Entity<Media>()
                .Property(m => m.CoverPath)
                .HasMaxLength(1024);

            modelBuilder.Entity<Media>()
                .Property(m => m.MediaType)
                .HasMaxLength(100);

            modelBuilder.Entity<Profile>()
                .Property(p => p.ProfileName)
                .HasMaxLength(255);

            modelBuilder.Entity<Artist>()
                .Property(a => a.ArtistName)
                .HasMaxLength(500);

            modelBuilder.Entity<Genre>()
                .Property(g => g.GenreName)
                .HasMaxLength(255);

            modelBuilder.Entity<Album>()
                .Property(a => a.Title)
                .HasMaxLength(500);

            modelBuilder.Entity<Track>()
                .Property(t => t.Title)
                .HasMaxLength(500);

            modelBuilder.Entity<Track>()
                .Property(t => t.FilePath)
                .HasMaxLength(2048);

            modelBuilder.Entity<Track>()
                .Property(t => t.FileType)
                .HasMaxLength(20);

            modelBuilder.Entity<Track>()
                .Property(t => t.Lyrics)
                .HasColumnType("text");

            modelBuilder.Entity<Playlist>()
                .Property(p => p.Title)
                .HasMaxLength(500);

            modelBuilder.Entity<CurrentQueue>()
                .Property(cq => cq.RepeatMode)
                .HasMaxLength(20);

            modelBuilder.Entity<GlobalConfig>()
                .Property(gc => gc.LanguagePreference)
                .HasMaxLength(10);
            modelBuilder.Entity<GlobalConfig>()
                .Property(gc => gc.WindowWidth);

            modelBuilder.Entity<GlobalConfig>()
                .Property(gc => gc.WindowHeight);

            modelBuilder.Entity<GlobalConfig>()
                .Property(gc => gc.WindowX)
                .HasDefaultValue(null);

            modelBuilder.Entity<GlobalConfig>()
                .Property(gc => gc.WindowY)
                .HasDefaultValue(null);

            modelBuilder.Entity<GlobalConfig>()
                .Property(gc => gc.IsWindowMaximized)
                .HasDefaultValue(false);

            modelBuilder.Entity<GlobalConfig>()
                .Property(gc => gc.EnableArtistApi)
                .HasDefaultValue(true);

            modelBuilder.Entity<Entities.Directory>()
                .Property(d => d.DirPath)
                .HasMaxLength(2048);

            // Configure JSON columns for PostgreSQL
            modelBuilder.Entity<ProfileConfig>()
                .Property(pc => pc.EqualizerPresets)
                .HasColumnType("jsonb") // JSONB for better performance
                .HasDefaultValue("{}");

            modelBuilder.Entity<ProfileConfig>()
                .Property(pc => pc.BlacklistDirectory)
                .HasColumnType("text[]")
                .HasColumnName("blacklistdirectory");

            modelBuilder.Entity<ProfileConfig>()
                .Property(pc => pc.ViewState)
                .HasColumnType("jsonb")
                .HasDefaultValue("{\"albums\": \"grid\", \"artists\": \"list\", \"library\": \"grid\"}");

            modelBuilder.Entity<ProfileConfig>()
                .Property(pc => pc.SortingState)
                .HasColumnType("jsonb")
                .HasDefaultValue("{\"library\": {\"field\": \"title\", \"order\": \"asc\"}}");

            modelBuilder.Entity<ProfileConfig>()
                .Property(pc => pc.NavigationExpanded)
                .HasDefaultValue(true);

            // PostgreSQL-specific default values using UTC functions
            modelBuilder.Entity<CurrentQueue>()
                .Property(cq => cq.LastModified)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<CurrentQueue>()
                .Property(cq => cq.IsShuffled)
                .HasDefaultValue(false);

            modelBuilder.Entity<CurrentQueue>()
                .Property(cq => cq.RepeatMode)
                .HasDefaultValue("none");

            // Default values for ProfileConfig
            modelBuilder.Entity<ProfileConfig>()
                .Property(pc => pc.LastVolume)
                .HasDefaultValue(50);

            modelBuilder.Entity<ProfileConfig>()
                .Property(pc => pc.Theme)
                .HasDefaultValue("dark");

            modelBuilder.Entity<ProfileConfig>()
                .Property(pc => pc.DynamicPause)
                .HasDefaultValue(true);

            // Default values for GlobalConfig
            modelBuilder.Entity<GlobalConfig>()
                .Property(gc => gc.LanguagePreference)
                .HasDefaultValue("en");

            // Default values for PlayCount
            modelBuilder.Entity<PlayCount>()
                .Property(pc => pc.Count)
                .HasDefaultValue(0);

            // Default values for QueueTrack
            modelBuilder.Entity<QueueTrack>()
                .Property(qt => qt.OriginalOrder)
                .HasDefaultValue(0);

            // Default values for Like using PostgreSQL UTC function
            modelBuilder.Entity<Like>()
                .Property(l => l.LikedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Default values for entity creation dates using PostgreSQL functions
            modelBuilder.Entity<Profile>()
                .Property(p => p.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<Profile>()
                .Property(p => p.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<Artist>()
                .Property(a => a.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<Artist>()
                .Property(a => a.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<Album>()
                .Property(a => a.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<Album>()
                .Property(a => a.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<Track>()
                .Property(t => t.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<Track>()
                .Property(t => t.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<Playlist>()
                .Property(p => p.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<Playlist>()
                .Property(p => p.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<PlayHistory>()
                .Property(ph => ph.PlayedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Configure PostgreSQL-specific optimizations
            ConfigurePostgreSqlOptimizations(modelBuilder);
        }

        private void ConfigurePostgreSqlOptimizations(ModelBuilder modelBuilder)
        {
            // Additional indexes for better query performance

            // Composite indexes for common queries
            modelBuilder.Entity<Track>()
                .HasIndex(t => new { t.AlbumId, t.TrackNumber })
                .HasDatabaseName("IX_Track_Album_TrackNumber");

            modelBuilder.Entity<Track>()
                .HasIndex(t => new { t.Title, t.AlbumId })
                .HasDatabaseName("IX_Track_Title_Album");

            modelBuilder.Entity<PlayHistory>()
                .HasIndex(ph => new { ph.ProfileId, ph.TrackId, ph.PlayedAt })
                .HasDatabaseName("IX_PlayHistory_Profile_Track_Date");

            // Partial indexes for better performance on nullable columns
            modelBuilder.Entity<Track>()
                .HasIndex(t => t.ReleaseDate)
                .HasDatabaseName("IX_Track_ReleaseDate")
                .HasFilter("releasedate IS NOT NULL");

            modelBuilder.Entity<Album>()
                .HasIndex(a => a.ReleaseDate)
                .HasDatabaseName("IX_Album_ReleaseDate")
                .HasFilter("releasedate IS NOT NULL");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // This method should typically not be used when DI is properly configured
            // It's here as a fallback only
            if (!optionsBuilder.IsConfigured)
            {
                // This should not happen in normal operation
                throw new InvalidOperationException("DbContext should be configured through dependency injection");
            }

            // Enable sensitive data logging in development
#if DEBUG
            optionsBuilder.EnableSensitiveDataLogging();
#endif
        }
    }
}