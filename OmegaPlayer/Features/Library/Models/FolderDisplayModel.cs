using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace OmegaPlayer.Features.Library.Models
{
    public partial class FolderDisplayModel : ObservableObject
    {
        public string FolderPath { get; set; }
        public string FolderName { get; set; }

        // Track-related properties
        public List<int> TrackIDs { get; set; } = new List<int>();
        public int TrackCount => TrackIDs.Count;
        public TimeSpan TotalDuration { get; set; }

        // For image loading management
        public string CoverSize { get; set; } = "low";

        [ObservableProperty]
        public Bitmap _cover;

        [ObservableProperty]
        private bool _isPointerOver;

        [ObservableProperty]
        private bool _isSelected;
    }
}