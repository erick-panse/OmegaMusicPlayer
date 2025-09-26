using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OmegaMusicPlayer.Infrastructure.Data.Entities
{
    /// <summary>
    /// Entity for media files (covers, photos, etc.)
    /// </summary>
    [Table("media")]
    public class Media
    {
        [Key]
        [Column("mediaid")]
        public int MediaId { get; set; }

        [Column("coverpath")]
        [MaxLength(1024)] 
        public string? CoverPath { get; set; }

        [Column("mediatype")]
        [MaxLength(100)] 
        public string? MediaType { get; set; }

        // Navigation properties
        public virtual ICollection<Track> TracksWithCover { get; set; } = new List<Track>();
        public virtual ICollection<Album> AlbumsWithCover { get; set; } = new List<Album>();
        public virtual ICollection<Artist> ArtistsWithPhoto { get; set; } = new List<Artist>();
        public virtual ICollection<Profile> ProfilesWithPhoto { get; set; } = new List<Profile>();
    }

    /// <summary>
    /// Entity for user profiles
    /// </summary>
    [Table("profile")]
    public class Profile
    {
        [Key]
        [Column("profileid")]
        public int ProfileId { get; set; }

        [Column("profilename")]
        [MaxLength(255)]
        public string? ProfileName { get; set; }

        [Column("createdat")]
        public DateTime CreatedAt { get; set; }

        [Column("updatedat")]
        public DateTime UpdatedAt { get; set; }

        [Column("photoid")]
        public int? PhotoId { get; set; }

        // Navigation properties
        [ForeignKey("PhotoId")]
        public virtual Media? Photo { get; set; }

        public virtual ICollection<Playlist> Playlists { get; set; } = new List<Playlist>();
        public virtual ICollection<CurrentQueue> CurrentQueues { get; set; } = new List<CurrentQueue>();
        public virtual ICollection<PlayHistory> PlayHistories { get; set; } = new List<PlayHistory>();
        public virtual ICollection<PlayCount> PlayCounts { get; set; } = new List<PlayCount>();
        public virtual ICollection<Like> Likes { get; set; } = new List<Like>();
        public virtual ProfileConfig? ProfileConfig { get; set; }
    }

    /// <summary>
    /// Entity for music artists
    /// </summary>
    [Table("artists")]
    public class Artist
    {
        [Key]
        [Column("artistid")]
        public int ArtistId { get; set; }

        [Column("artistname")]
        [MaxLength(500)]
        public string? ArtistName { get; set; }

        [Column("photoid")]
        public int? PhotoId { get; set; }

        [Column("bio")]
        public string? Bio { get; set; } 

        [Column("createdat")]
        public DateTime? CreatedAt { get; set; }

        [Column("updatedat")]
        public DateTime? UpdatedAt { get; set; }

        [Column("lastapidatasearch")]
        public DateTime? LastApiDataSearch { get; set; }

        // Navigation properties
        [ForeignKey("PhotoId")]
        public virtual Media? Photo { get; set; }

        public virtual ICollection<Album> Albums { get; set; } = new List<Album>();
        public virtual ICollection<TrackArtist> TrackArtists { get; set; } = new List<TrackArtist>();
    }

    /// <summary>
    /// Entity for music genres
    /// </summary>
    [Table("genre")]
    public class Genre
    {
        [Key]
        [Column("genreid")]
        public int GenreId { get; set; }

        [Column("genrename")]
        [MaxLength(255)]
        public string? GenreName { get; set; }

        // Navigation properties
        public virtual ICollection<TrackGenre> TrackGenres { get; set; } = new List<TrackGenre>();
    }

    /// <summary>
    /// Entity for music albums
    /// </summary>
    [Table("albums")]
    public class Album
    {
        [Key]
        [Column("albumid")]
        public int AlbumId { get; set; }

        [Column("title")]
        [MaxLength(500)]
        public string? Title { get; set; }

        [Column("artistid")]
        public int? ArtistId { get; set; }

        [Column("releasedate")]
        public DateTime? ReleaseDate { get; set; }

        [Column("discnumber")]
        public int? DiscNumber { get; set; }

        [Column("trackcounter")]
        public int? TrackCounter { get; set; }

        [Column("coverid")]
        public int? CoverId { get; set; }

        [Column("createdat")]
        public DateTime? CreatedAt { get; set; }

        [Column("updatedat")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey("ArtistId")]
        public virtual Artist? Artist { get; set; }

        [ForeignKey("CoverId")]
        public virtual Media? Cover { get; set; }

        public virtual ICollection<Track> Tracks { get; set; } = new List<Track>();
    }

    /// <summary>
    /// Entity for music tracks
    /// </summary>
    [Table("tracks")]
    public class Track
    {
        [Key]
        [Column("trackid")]
        public int TrackId { get; set; }

        [Column("title")]
        [MaxLength(500)]
        public string? Title { get; set; }

        [Column("albumid")]
        public int? AlbumId { get; set; }

        [Column("duration")]
        public long? DurationTicks { get; set; }

        // Helper property for TimeSpan conversion
        [NotMapped]
        public TimeSpan? Duration
        {
            get => DurationTicks.HasValue ? new TimeSpan(DurationTicks.Value) : null;
            set => DurationTicks = value?.Ticks;
        }

        [Column("releasedate")]
        public DateTime? ReleaseDate { get; set; } 

        [Column("tracknumber")]
        public int? TrackNumber { get; set; }

        [Column("filepath")]
        [MaxLength(2048)]
        public string? FilePath { get; set; }

        [Column("lyrics")]
        public string? Lyrics { get; set; } 

        [Column("bitrate")]
        public int? Bitrate { get; set; }

        [Column("filesize")]
        public long? FileSize { get; set; }

        [Column("filetype")]
        [MaxLength(20)]
        public string? FileType { get; set; }

        [Column("createdat")]
        public DateTime? CreatedAt { get; set; }

        [Column("updatedat")]
        public DateTime? UpdatedAt { get; set; }

        [Column("coverid")]
        public int? CoverId { get; set; }

        [Column("genreid")]
        public int? GenreId { get; set; }

        // Navigation properties
        [ForeignKey("AlbumId")]
        public virtual Album? Album { get; set; }

        [ForeignKey("CoverId")]
        public virtual Media? Cover { get; set; }

        public virtual ICollection<TrackArtist> TrackArtists { get; set; } = new List<TrackArtist>();
        public virtual ICollection<TrackGenre> TrackGenres { get; set; } = new List<TrackGenre>();
        public virtual ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();
        public virtual ICollection<QueueTrack> QueueTracks { get; set; } = new List<QueueTrack>();
        public virtual ICollection<PlayHistory> PlayHistories { get; set; } = new List<PlayHistory>();
        public virtual ICollection<PlayCount> PlayCounts { get; set; } = new List<PlayCount>();
        public virtual ICollection<Like> Likes { get; set; } = new List<Like>();
    }

    /// <summary>
    /// Entity for playlists
    /// </summary>
    [Table("playlists")]
    public class Playlist
    {
        [Key]
        [Column("playlistid")]
        public int PlaylistId { get; set; }

        [Column("profileid")]
        public int ProfileId { get; set; }

        [Column("title")]
        [MaxLength(500)]
        public string? Title { get; set; }

        [Column("createdat")]
        public DateTime CreatedAt { get; set; }

        [Column("updatedat")]
        public DateTime UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey("ProfileId")]
        public virtual Profile Profile { get; set; }

        public virtual ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();
    }

    /// <summary>
    /// Junction entity for Track-Artist many-to-many relationship
    /// </summary>
    [Table("trackartist")]
    public class TrackArtist
    {
        [Key, Column("trackid", Order = 0)]
        public int TrackId { get; set; }

        [Key, Column("artistid", Order = 1)]
        public int ArtistId { get; set; }

        // Navigation properties
        [ForeignKey("TrackId")]
        public virtual Track Track { get; set; } = null!;

        [ForeignKey("ArtistId")]
        public virtual Artist Artist { get; set; } = null!;
    }

    /// <summary>
    /// Junction entity for Track-Genre many-to-many relationship
    /// </summary>
    [Table("trackgenre")]
    public class TrackGenre
    {
        [Key, Column("trackid", Order = 0)]
        public int TrackId { get; set; }

        [Key, Column("genreid", Order = 1)]
        public int GenreId { get; set; }

        // Navigation properties
        [ForeignKey("TrackId")]
        public virtual Track Track { get; set; } = null!;

        [ForeignKey("GenreId")]
        public virtual Genre Genre { get; set; } = null!;
    }

    /// <summary>
    /// Entity for playlist tracks with order
    /// </summary>
    [Table("playlisttracks")]
    public class PlaylistTrack
    {
        [Key, Column("playlistid", Order = 0)]
        public int PlaylistId { get; set; }

        [Key, Column("trackorder", Order = 1)]
        public int TrackOrder { get; set; }

        [Column("trackid")]
        public int TrackId { get; set; }

        // Navigation properties
        [ForeignKey("PlaylistId")]
        public virtual Playlist Playlist { get; set; } = null!;

        [ForeignKey("TrackId")]
        public virtual Track Track { get; set; }
    }

    /// <summary>
    /// Entity for current playback queue
    /// </summary>
    [Table("currentqueue")]
    public class CurrentQueue
    {
        [Key]
        [Column("queueid")]
        public int QueueId { get; set; }

        [Column("profileid")]
        public int ProfileId { get; set; }

        [Column("currenttrackorder")]
        public int? CurrentTrackOrder { get; set; }

        [Column("lastmodified")]
        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        [Column("isshuffled")]
        public bool IsShuffled { get; set; } = false;

        [Column("repeatmode")]
        [MaxLength(20)] // Increased
        public string RepeatMode { get; set; } = "none";

        // Navigation properties
        [ForeignKey("ProfileId")]
        public virtual Profile Profile { get; set; } = null!;

        public virtual ICollection<QueueTrack> QueueTracks { get; set; } = new List<QueueTrack>();
    }

    /// <summary>
    /// Entity for tracks in the queue
    /// </summary>
    [Table("queuetracks")]
    public class QueueTrack
    {
        [Key, Column("queueid", Order = 0)]
        public int QueueId { get; set; }

        [Key, Column("trackorder", Order = 1)]
        public int TrackOrder { get; set; }

        [Column("trackid")]
        public int TrackId { get; set; }

        [Column("originalorder")]
        public int OriginalOrder { get; set; } = 0;

        // Navigation properties
        [ForeignKey("QueueId")]
        public virtual CurrentQueue CurrentQueue { get; set; } = null!;

        [ForeignKey("TrackId")]
        public virtual Track? Track { get; set; }
    }

    /// <summary>
    /// Entity for global application configuration
    /// </summary>
    [Table("globalconfig")]
    public class GlobalConfig
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("lastusedprofile")]
        public int? LastUsedProfile { get; set; }

        [Column("languagepreference")]
        [MaxLength(10)]
        public string LanguagePreference { get; set; } = "en";

        // Window state properties
        [Column("windowwidth")]
        public int WindowWidth { get; set; } = 1440;

        [Column("windowheight")]
        public int WindowHeight { get; set; } = 760;

        [Column("windowx")]
        public int? WindowX { get; set; }

        [Column("windowy")]
        public int? WindowY { get; set; }

        [Column("iswindowmaximized")]
        public bool IsWindowMaximized { get; set; } = false;

        // API settings
        [Column("enableartistapi")]
        public bool EnableArtistApi { get; set; } = true;

        // Navigation properties
        [ForeignKey("LastUsedProfile")]
        public virtual Profile? LastUsedProfileNavigation { get; set; }
    }

    /// <summary>
    /// Entity for profile-specific configuration
    /// </summary>
    [Table("profileconfig")]
    public class ProfileConfig
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("profileid")]
        public int? ProfileId { get; set; }

        [Column("equalizerpresets")]
        public string EqualizerPresets { get; set; } = "{}";

        [Column("lastvolume")]
        public int LastVolume { get; set; } = 50;

        [Column("theme")]
        public string Theme { get; set; } = "dark";

        [Column("dynamicpause")]
        public bool DynamicPause { get; set; } = true;

        [Column("blacklistdirectory")]
        public string[]? BlacklistDirectory { get; set; }

        [Column("viewstate")]
        public string ViewState { get; set; } = "{\"albums\": \"grid\", \"artists\": \"list\", \"library\": \"grid\"}";

        [Column("sortingstate")]
        public string SortingState { get; set; } = "{\"library\": {\"field\": \"title\", \"order\": \"asc\"}}";

        [Column("navigationexpanded")]
        public bool NavigationExpanded { get; set; } = true;

        // Navigation properties
        [ForeignKey("ProfileId")]
        public virtual Profile? Profile { get; set; }
    }

    /// <summary>
    /// Entity for directory paths
    /// </summary>
    [Table("directories")]
    public class Directory
    {
        [Key]
        [Column("dirid")]
        public int DirId { get; set; }

        [Column("dirpath")]
        [MaxLength(2048)]
        public string? DirPath { get; set; }
    }

    /// <summary>
    /// Entity for tracking play history
    /// </summary>
    [Table("playhistory")]
    public class PlayHistory
    {
        [Key]
        [Column("historyid")]
        public int HistoryId { get; set; }

        [Column("profileid")]
        public int ProfileId { get; set; }

        [Column("trackid")]
        public int TrackId { get; set; }

        [Column("playedat")]
        public DateTime PlayedAt { get; set; }

        // Navigation properties
        [ForeignKey("ProfileId")]
        public virtual Profile Profile { get; set; } = null!;

        [ForeignKey("TrackId")]
        public virtual Track Track { get; set; } = null!;
    }

    /// <summary>
    /// Entity for tracking play counts per profile
    /// </summary>
    [Table("playcounts")]
    public class PlayCount
    {
        [Key, Column("profileid", Order = 0)]
        public int ProfileId { get; set; }

        [Key, Column("trackid", Order = 1)]
        public int TrackId { get; set; }

        [Column("playcount")]
        public int Count { get; set; } = 0;

        [Column("lastplayed")]
        public DateTime? LastPlayed { get; set; }

        // Navigation properties
        [ForeignKey("ProfileId")]
        public virtual Profile Profile { get; set; } = null!;

        [ForeignKey("TrackId")]
        public virtual Track Track { get; set; } = null!;
    }

    /// <summary>
    /// Entity for tracking liked tracks per profile
    /// </summary>
    [Table("likes")]
    public class Like
    {
        [Key, Column("profileid", Order = 0)]
        public int ProfileId { get; set; }

        [Key, Column("trackid", Order = 1)]
        public int TrackId { get; set; }

        [Column("likedat")]
        public DateTime LikedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("ProfileId")]
        public virtual Profile Profile { get; set; } = null!;

        [ForeignKey("TrackId")]
        public virtual Track Track { get; set; } = null!;
    }
}