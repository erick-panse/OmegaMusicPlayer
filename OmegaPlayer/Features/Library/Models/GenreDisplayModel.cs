using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace OmegaPlayer.Features.Library.Models
{
    public partial class GenreDisplayModel : ObservableObject
    {
        public int GenreID { get; set; }
        public string Name { get; set; }
        public string PhotoPath { get; set; }
        public Bitmap Photo { get; set; }

        // Track-related properties
        public List<int> TrackIDs { get; set; } = new List<int>();
        public int TrackCount => TrackIDs.Count;
        public TimeSpan TotalDuration { get; set; }

        // For image loading management
        public string PhotoSize { get; set; } = "low";

        [ObservableProperty]
        private bool _isPointerOver;

        [ObservableProperty]
        private bool _isSelected;
    }
}