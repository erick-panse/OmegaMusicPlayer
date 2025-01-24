using Avalonia.Media.Imaging;
using System;

namespace OmegaPlayer.Features.Profile.Models
{
    public class Profiles
    {
        public int ProfileID { get; set; }
        public string ProfileName { get; set; }
        public int ConfigID { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int PhotoID { get; set; }
        public Bitmap Photo { get; set; }
    }
}
