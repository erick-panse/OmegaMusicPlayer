using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace OmegaPlayer.Features.Profile.Models
{
    public partial class Profiles : ObservableObject
    {
        public int ProfileID { get; set; }
        public string ProfileName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int PhotoID { get; set; }

        [ObservableProperty]
        public Bitmap _photo;

        [ObservableProperty]
        private bool _canBeDeleted = true;
    }
}
