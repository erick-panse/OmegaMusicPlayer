using System;

namespace OmegaMusicPlayer.Features.Library.Models
{
    public class PlayHistory
    {
        public int HistoryID { get; set; }
        public int ProfileID { get; set; }
        public int TrackID { get; set; }
        public DateTime PlayedAt { get; set; }
    }
}
