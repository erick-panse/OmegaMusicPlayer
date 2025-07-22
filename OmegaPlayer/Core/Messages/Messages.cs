using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.ViewModels;

namespace OmegaPlayer.Core.Messages
{

    public class NavigationRequestMessage
    {
        public ContentType ContentType { get; }
        public object Data { get; }

        public NavigationRequestMessage(ContentType contentType, object data)
        {
            ContentType = contentType;
            Data = data;
        }

    }
    /// <summary>
    /// Message for notifying that the current playing track has changed
    /// </summary>
    public class CurrentTrackChangedMessage
    {
        public TrackDisplayModel CurrentTrack { get; }

        public CurrentTrackChangedMessage(TrackDisplayModel currentTrack)
        {
            CurrentTrack = currentTrack;
        }
    }

    /// <summary>
    /// Message for notifying that a profile's configuration has changed
    /// </summary>
    public class ProfileChangedMessage { };

    /// <summary>
    /// Message for notifying Main view to show Lyrics
    /// </summary>
    public class ShowLyricsMessage { };

    /// <summary>
    /// Message for notifying that AllTracks was Invalidated
    /// </summary>
    public class AllTracksInvalidatedMessage { };

}
