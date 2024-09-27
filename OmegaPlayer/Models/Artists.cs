using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace OmegaPlayer.Models
{
    public class Artists
    {
        public int ArtistID { get; set; }
        public string ArtistName { get; set; }
        public int PhotoID { get; set; }
        public string Bio { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsLastArtist { get; set; } = true;
    }
}
