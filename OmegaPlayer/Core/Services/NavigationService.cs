using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Features.Shell.ViewModels;
using System.Collections.Generic;
using System;

namespace OmegaPlayer.Core.Navigation.Services
{
    public interface INavigationService
    {
        event EventHandler<NavigationEventArgs> NavigationRequested;
        void NavigateToNowPlaying(TrackDisplayModel currentTrack, List<TrackDisplayModel> tracks, int currentIndex);
        bool IsCurrentlyShowingNowPlaying();
        void ClearCurrentView();  // Add this line
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

        public void NavigateToNowPlaying(TrackDisplayModel currentTrack, List<TrackDisplayModel> tracks, int currentIndex)
        {
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

        public bool IsCurrentlyShowingNowPlaying()
        {
            if (_currentContentType != ContentType.NowPlaying) return false;

            var nowPlayingInfo = _currentData as NowPlayingInfo;
            return nowPlayingInfo != null;
        }

        public void ClearCurrentView()
        {
            _currentContentType = default;
            _currentData = null;
        }
    }
}