using System;

namespace OmegaMusicPlayer.Features.Playlists.Models
{
    public class Playlist
    {
        public int PlaylistID { get; set; }
        public int ProfileID { get; set; }
        public string Title { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
