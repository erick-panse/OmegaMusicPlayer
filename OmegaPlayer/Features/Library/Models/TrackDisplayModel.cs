using System.Collections.Generic;
using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Core.ViewModels;

namespace OmegaPlayer.Features.Library.Models{
    public partial class TrackDisplayModel : ViewModelBase
    {
        public int TrackID { get; set; }
        public string Title { get; set; }
        public string AlbumTitle { get; set; }
        public List<Artists> Artists { get; set; } // To store multiple artists
        public TimeSpan Duration { get; set; }
        public bool IsLiked { get; set; }
        public string FilePath { get; set; }
        public string CoverPath { get; set; }
        public int CoverID { get; set; }  // Add this property
        public string Genre { get; set; }
        public DateTime ReleaseDate { get; set; }
        public int PlayCount { get; set; }

        // property for thumbnail image
        public Bitmap Thumbnail { get; set; }

        // Optional property to track the resolution of the loaded image (e.g., low or high)
        public string ThumbnailSize { get; set; } = "low";

        [ObservableProperty]
        private bool _isPointerOver;

        [ObservableProperty]
        private bool _isSelected;

        public void ToggleSelected()
        {
            //IsSelected = !IsSelected;
        }

    }
}