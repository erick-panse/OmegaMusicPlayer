using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace OmegaPlayer.Features.Library.Models
{
    public partial class PlaylistDisplayModel : ObservableObject
    {
        public int PlaylistID { get; set; }
        public string Title { get; set; }
        public int ProfileID { get; set; }
        public string CoverPath { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Track-related properties
        public List<int> TrackIDs { get; set; } = new List<int>();
        public int TrackCount => TrackIDs?.Count ?? 0;
        public TimeSpan TotalDuration { get; set; }

        // For image loading management
        public string CoverSize { get; set; } = "low";
        public bool IsFavoritePlaylist { get; set; } = false;

        [ObservableProperty]
        public Bitmap _cover;

        [ObservableProperty]
        private bool _isPointerOver;

        [ObservableProperty]
        private bool _isSelected;
    }
}