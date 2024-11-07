using System;

namespace OmegaPlayer.Features.UserProfile.Models
{
    public class Profile
    {
        public int ProfileID { get; set; }
        public string ProfileName { get; set; }
        public int ConfigID { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int PhotoID { get; set; }
    }
}
