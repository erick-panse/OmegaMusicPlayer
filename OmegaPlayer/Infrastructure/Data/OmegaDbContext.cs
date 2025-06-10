using Microsoft.EntityFrameworkCore;
using OmegaPlayer.Infrastructure.Data.Entities;
using System;
using System.IO;

namespace OmegaPlayer.Infrastructure.Data
{
    /// <summary>
    /// Entity Framework DbContext for OmegaPlayer SQLite database
    /// </summary>
    public class OmegaPlayerDbContext : DbContext
    {
        public OmegaPlayerDbContext(DbContextOptions<OmegaPlayerDbContext> options) : base(options)
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
        public DbSet<BlacklistedDirectory> BlacklistedDirectories { get; set; }
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
                .HasKey(pt => pt.PlaylistId);

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

            modelBuilder.Entity<BlacklistedDirectory>()
                .HasIndex(bd => new { bd.ProfileId, bd.Path })
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

            // Configure default values and constraints
            ConfigureDefaults(modelBuilder);
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

            modelBuilder.Entity<BlacklistedDirectory>()
                .HasOne(bd => bd.Profile)
                .WithMany(p => p.BlacklistedDirectories)
                .HasForeignKey(bd => bd.ProfileId)
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

        private void ConfigureDefaults(ModelBuilder modelBuilder)
        {
            // Configure string length constraints for SQLite (optional but good practice)
            modelBuilder.Entity<Media>()
                .Property(m => m.CoverPath)
                .HasMaxLength(512);

            modelBuilder.Entity<Media>()
                .Property(m => m.MediaType)
                .HasMaxLength(50);

            modelBuilder.Entity<Profile>()
                .Property(p => p.ProfileName)
                .HasMaxLength(255);

            modelBuilder.Entity<Artist>()
                .Property(a => a.ArtistName)
                .HasMaxLength(255);

            modelBuilder.Entity<Genre>()
                .Property(g => g.GenreName)
                .HasMaxLength(255);

            modelBuilder.Entity<Album>()
                .Property(a => a.Title)
                .HasMaxLength(255);

            modelBuilder.Entity<Track>()
                .Property(t => t.Title)
                .HasMaxLength(255);

            modelBuilder.Entity<Track>()
                .Property(t => t.FilePath)
                .HasMaxLength(500);

            modelBuilder.Entity<Track>()
                .Property(t => t.FileType)
                .HasMaxLength(10);

            modelBuilder.Entity<Playlist>()
                .Property(p => p.Title)
                .HasMaxLength(255);

            modelBuilder.Entity<CurrentQueue>()
                .Property(cq => cq.RepeatMode)
                .HasMaxLength(10);

            modelBuilder.Entity<GlobalConfig>()
                .Property(gc => gc.LanguagePreference)
                .HasMaxLength(10);

            modelBuilder.Entity<BlacklistedDirectory>()
                .Property(bd => bd.Path)
                .HasMaxLength(1024);

            modelBuilder.Entity<Entities.Directory>()
                .Property(d => d.DirPath)
                .HasMaxLength(1000);

            // Default values for CurrentQueue
            modelBuilder.Entity<CurrentQueue>()
                .Property(cq => cq.LastModified)
                .HasDefaultValue(DateTime.UtcNow);

            modelBuilder.Entity<CurrentQueue>()
                .Property(cq => cq.IsShuffled)
                .HasDefaultValue(false);

            modelBuilder.Entity<CurrentQueue>()
                .Property(cq => cq.RepeatMode)
                .HasDefaultValue("none");

            // Default values for ProfileConfig
            modelBuilder.Entity<ProfileConfig>()
                .Property(pc => pc.EqualizerPresets)
                .HasDefaultValue("{}");

            modelBuilder.Entity<ProfileConfig>()
                .Property(pc => pc.LastVolume)
                .HasDefaultValue(50);

            modelBuilder.Entity<ProfileConfig>()
                .Property(pc => pc.Theme)
                .HasDefaultValue("dark");

            modelBuilder.Entity<ProfileConfig>()
                .Property(pc => pc.DynamicPause)
                .HasDefaultValue(true);

            modelBuilder.Entity<ProfileConfig>()
                .Property(pc => pc.ViewState)
                .HasDefaultValue("{\"albums\": \"grid\", \"artists\": \"list\", \"library\": \"grid\"}");

            modelBuilder.Entity<ProfileConfig>()
                .Property(pc => pc.SortingState)
                .HasDefaultValue("{\"library\": {\"field\": \"title\", \"order\": \"asc\"}}");

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

            // Default values for Like
            modelBuilder.Entity<Like>()
                .Property(l => l.LikedAt)
                .HasDefaultValue(DateTime.UtcNow);

            // Default values for entity creation dates
            modelBuilder.Entity<Profile>()
                .Property(p => p.CreatedAt)
                .HasDefaultValue(DateTime.UtcNow);

            modelBuilder.Entity<Profile>()
                .Property(p => p.UpdatedAt)
                .HasDefaultValue(DateTime.UtcNow);

            modelBuilder.Entity<Artist>()
                .Property(a => a.CreatedAt)
                .HasDefaultValue(DateTime.UtcNow);

            modelBuilder.Entity<Artist>()
                .Property(a => a.UpdatedAt)
                .HasDefaultValue(DateTime.UtcNow);

            modelBuilder.Entity<Album>()
                .Property(a => a.CreatedAt)
                .HasDefaultValue(DateTime.UtcNow);

            modelBuilder.Entity<Album>()
                .Property(a => a.UpdatedAt)
                .HasDefaultValue(DateTime.UtcNow);

            modelBuilder.Entity<Track>()
                .Property(t => t.CreatedAt)
                .HasDefaultValue(DateTime.UtcNow);

            modelBuilder.Entity<Track>()
                .Property(t => t.UpdatedAt)
                .HasDefaultValue(DateTime.UtcNow);

            modelBuilder.Entity<Playlist>()
                .Property(p => p.CreatedAt)
                .HasDefaultValue(DateTime.UtcNow);

            modelBuilder.Entity<Playlist>()
                .Property(p => p.UpdatedAt)
                .HasDefaultValue(DateTime.UtcNow);

            modelBuilder.Entity<PlayHistory>()
                .Property(ph => ph.PlayedAt)
                .HasDefaultValue(DateTime.UtcNow);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // This is a fallback - should normally be configured in DI
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var omegaPath = Path.Combine(appDataPath, "OmegaPlayer");
                System.IO.Directory.CreateDirectory(omegaPath);
                var dbFilePath = Path.Combine(omegaPath, "OmegaPlayer.db");

                optionsBuilder.UseSqlite($"Data Source={dbFilePath}");
            }
        }
    }
}