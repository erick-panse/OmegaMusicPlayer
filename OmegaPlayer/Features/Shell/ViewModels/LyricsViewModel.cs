using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Messages;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Infrastructure.Services;

namespace OmegaPlayer.Features.Shell.ViewModels
{
    public partial class LyricsViewModel : ViewModelBase
    {
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly LocalizationService _localizationService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private string _trackLyrics = string.Empty;

        public LyricsViewModel(
            TrackQueueViewModel trackQueueViewModel, 
            LocalizationService localizationService,
            IMessenger messenger)
        {
            _trackQueueViewModel = trackQueueViewModel;
            _localizationService = localizationService;
            _messenger = messenger;

            // Subscribe to track changes
            _messenger.Register<CurrentTrackChangedMessage>(this, (r, m) =>
            {
                UpdateLyricsForTrack(m.CurrentTrack);
            });
        }

        public void InitializeProperties()
        {
            var track = _trackQueueViewModel.CurrentTrack;
            if (track == null)
            {
                TrackLyrics = _localizationService["NoTrackPlaying"];
                return;
            }

            TrackLyrics = !string.IsNullOrWhiteSpace(track.Lyrics)
                ? track.Lyrics
                : _localizationService["NoLyrics"];
        }
        private void UpdateLyricsForTrack(TrackDisplayModel track)
        {
            if (track == null)
            {
                TrackLyrics = _localizationService["NoTrackPlaying"];
                return;
            }

            TrackLyrics = !string.IsNullOrWhiteSpace(track.Lyrics)
                ? track.Lyrics
                : _localizationService["NoLyrics"];

        }

    }
}