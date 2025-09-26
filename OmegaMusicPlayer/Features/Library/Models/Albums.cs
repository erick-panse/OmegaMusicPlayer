using System;

namespace OmegaMusicPlayer.Features.Library.Models
{
    public class Albums
    {
        public int AlbumID { get; set; }
        public string Title { get; set; }
        public int ArtistID { get; set; }
        public DateTime ReleaseDate { get; set; }
        public int DiscNumber { get; set; }
        public int TrackCounter { get; set; }
        public int CoverID { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
