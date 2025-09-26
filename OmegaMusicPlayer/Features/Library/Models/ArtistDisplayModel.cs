using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace OmegaMusicPlayer.Features.Library.Models
{
    public partial class ArtistDisplayModel : ObservableObject
    {
        public int ArtistID { get; set; }
        public string Name { get; set; }
        public string PhotoPath { get; set; }
        public string Bio { get; set; } // Artist biography/description

        // Track-related properties
        public List<int> TrackIDs { get; set; } = new List<int>();
        public int TrackCount => TrackIDs.Count;
        public TimeSpan TotalDuration { get; set; }

        // For image loading management
        public string PhotoSize { get; set; } = "low";

        [ObservableProperty]
        public Bitmap _photo;

        [ObservableProperty]
        private bool _isPointerOver;

        [ObservableProperty]
        private bool _isSelected;
    }

}
