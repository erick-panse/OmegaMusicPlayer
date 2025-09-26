using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmegaMusicPlayer.Features.Playback.Models
{
    public class QueueWithTracks
    {
        public CurrentQueue CurrentQueueByProfile { get; set; }
        public List<QueueTracks> Tracks { get; set; }
    }
}
