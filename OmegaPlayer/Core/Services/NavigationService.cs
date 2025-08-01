using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Core.Enums.LibraryEnums;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Playback.ViewModels;
using System;
using System.Collections.Generic;

namespace OmegaPlayer.Core.Navigation.Services
{
    public interface INavigationService
    {
        event EventHandler<NavigationEventArgs> NavigationRequested;

        /// <summary>
        /// Event fired before navigation changes, allowing subscribers to clean up
        /// </summary>
        event EventHandler<NavigationEventArgs> BeforeNavigationChange;

        void NavigateToNowPlaying(TrackDisplayModel currentTrack, List<TrackDisplayModel> tracks, int currentIndex);
        void NavigateToArtistDetails(ArtistDisplayModel artist);
        void NavigateToAlbumDetails(AlbumDisplayModel albumID);
        bool IsCurrentlyShowingNowPlaying();
        void ClearCurrentView();

        /// <summary>
        /// Notifies subscribers that navigation is about to change
        /// </summary>
        void NotifyBeforeNavigationChange(ContentType newContentType, object newData = null);
    }

    public class NavigationEventArgs : EventArgs
    {
        public ContentType Type { get; set; }
        public object Data { get; set; }
    }

    public class NavigationService : INavigationService
    {
        private ContentType _currentContentType;
        private object _currentData;

        public event EventHandler<NavigationEventArgs> NavigationRequested;
        public event EventHandler<NavigationEventArgs> BeforeNavigationChange;

        /// <summary>
        /// Notifies that navigation is about to change, allowing cleanup of resources
        /// </summary>
        public void NotifyBeforeNavigationChange(ContentType newContentType, object newData = null)
        {
            // Skip if it's the same content type and we're not forcing a refresh
            if (newContentType == _currentContentType && newData == null)
                return;

            BeforeNavigationChange?.Invoke(this, new NavigationEventArgs
            {
                Type = newContentType,
                Data = newData
            });
        }

        public void NavigateToNowPlaying(TrackDisplayModel currentTrack, List<TrackDisplayModel> tracks, int currentIndex)
        {
            // Notify before navigation change
            NotifyBeforeNavigationChange(ContentType.NowPlaying);

            _currentContentType = ContentType.NowPlaying;
            _currentData = new NowPlayingInfo
            {
                CurrentTrack = currentTrack,
                AllTracks = tracks,
                CurrentTrackIndex = currentIndex
            };

            NavigationRequested?.Invoke(this, new NavigationEventArgs
            {
                Type = _currentContentType,
                Data = _currentData
            });
        }

        public void NavigateToArtistDetails(ArtistDisplayModel artist)
        {
            // Notify before navigation change
            NotifyBeforeNavigationChange(ContentType.Artist);

            _currentContentType = ContentType.Artist;
            _currentData = artist;

            NavigationRequested?.Invoke(this, new NavigationEventArgs
            {
                Type = _currentContentType,
                Data = _currentData
            });
        }

        public void NavigateToAlbumDetails(AlbumDisplayModel album)
        {
            // Notify before navigation change
            NotifyBeforeNavigationChange(ContentType.Album);

            _currentContentType = ContentType.Album;
            _currentData = album;

            NavigationRequested?.Invoke(this, new NavigationEventArgs
            {
                Type = _currentContentType,
                Data = _currentData
            });
        }

        public bool IsCurrentlyShowingNowPlaying()
        {
            if (_currentContentType != ContentType.NowPlaying) return false;

            var nowPlayingInfo = _currentData as NowPlayingInfo;
            return nowPlayingInfo != null;
        }

        public void ClearCurrentView()
        {
            // If we're clearing the current view, notify with a 'null' content type
            // This allows subscribers to clean up resources regardless of next destination
            if (_currentContentType != default)
            {
                NotifyBeforeNavigationChange(default);
            }

            _currentContentType = default;
            _currentData = null;
        }

        public class NavigationHistoryItem
        {
            public string Destination { get; set; }           // "details", "library", "home", etc.
            public ContentType ContentType { get; set; }      // The actual content type (Artist, Album, Library, etc.)
            public ContentType? DetailsContentType { get; set; } // For details views: the specific type being shown
            public object Data { get; set; }                  // Navigation data (artist object, album object, etc.)
            public ViewModelBase ViewModel { get; set; }
        }
    }
}