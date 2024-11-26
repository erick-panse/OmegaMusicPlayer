using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace OmegaPlayer.Features.Library.Models
{
    public partial class AlbumDisplayModel : ObservableObject
    {
        public int AlbumID { get; set; }
        public string Title { get; set; }
        public string ArtistName { get; set; }
        public int ArtistID { get; set; }
        public string CoverPath { get; set; }
        public Bitmap Cover { get; set; }
        public DateTime ReleaseDate { get; set; }

        // Track-related properties
        public List<int> TrackIDs { get; set; } = new List<int>();
        public int TrackCount => TrackIDs.Count;
        public TimeSpan TotalDuration { get; set; }

        // For image loading management
        public string CoverSize { get; set; } = "low";

        [ObservableProperty]
        private bool _isPointerOver;

        [ObservableProperty]
        private bool _isSelected;
    }

}
