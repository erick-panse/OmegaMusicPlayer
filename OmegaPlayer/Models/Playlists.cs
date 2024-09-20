using System;

namespace OmegaPlayer.Models
{
    public class Playlists
    {
        public int PlaylistID { get; set; }
        public int ProfileID { get; set; }
        public string Title { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
