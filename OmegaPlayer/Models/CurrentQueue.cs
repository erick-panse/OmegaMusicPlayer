using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmegaPlayer.Models
{
    public class CurrentQueue
    {
        public int QueueID { get; set; }
        public int ProfileID { get; set; }
        public int CurrentTrackOrder { get; set; }
    }
}
