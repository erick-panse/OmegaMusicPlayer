using System;

namespace OmegaPlayer.Features.Library.Models
{
    public class Tracks
    {
        public int TrackID { get; set; }
        public string Title { get; set; }
        public int AlbumID { get; set; }
        public TimeSpan Duration { get; set; }
        public int TrackNumber { get; set; }
        public string FilePath { get; set; }
        public string Lyrics { get; set; }
        public int BitRate { get; set; }
        public string FileType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int PlayCount { get; set; }
        public int CoverID { get; set; }
        public int GenreID { get; set; }
        public DateTime ReleaseDate { get; set; }
        public int FileSize { get; set; }
    }
}
