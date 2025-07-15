using CommunityToolkit.Mvvm.ComponentModel;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Infrastructure.Services;

namespace OmegaPlayer.Features.Shell.ViewModels
{
    public partial class LyricsViewModel : ViewModelBase
    {
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly LocalizationService _localizationService;

        [ObservableProperty]
        private string _trackLyrics = string.Empty;

        public LyricsViewModel(TrackQueueViewModel trackQueueViewModel, LocalizationService localizationService)
        {
            _trackQueueViewModel = trackQueueViewModel;
            _localizationService = localizationService;
        }

        public void InitializeProperties()
        {
            var track = _trackQueueViewModel.CurrentTrack;
            if (track == null) return;

            TrackLyrics = track?.Lyrics ?? _localizationService["NoLyrics"];
        }
    }
}