using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Core.Enums.LibraryEnums;

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
    /// Message for notifying Main view to show ImageMode
    /// </summary>
    public class ShowImageModeMessage { };

    /// <summary>
    /// Message for notifying that AllTracks was Invalidated
    /// </summary>
    public class AllTracksInvalidatedMessage { };

    /// <summary>
    /// Message sent when playlists are created, updated, or deleted
    /// </summary>
    public class PlaylistUpdateMessage { };

    /// <summary>
    /// Message to request scrolling to a specific track in the track list
    /// </summary>
    public class ScrollToTrackMessage
    {
        public int TrackIndex { get; }

        public ScrollToTrackMessage(int trackIndex)
        {
            TrackIndex = trackIndex;
        }
    }
}
