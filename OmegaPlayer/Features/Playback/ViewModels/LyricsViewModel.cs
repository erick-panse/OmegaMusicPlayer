using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Messages;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.UI;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Playback.ViewModels
{
    public partial class LyricsViewModel : ViewModelBase
    {
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly LocalizationService _localizationService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private string _trackLyrics = string.Empty;

        [ObservableProperty]
        private Bitmap _currentTrackImage;

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
                _ = UpdateLyricsForTrack(m.CurrentTrack);
            });
        }

        public async Task InitializeProperties()
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

            // Try to load high quality image
            var trackDisplayService = App.ServiceProvider.GetRequiredService<TrackDisplayService>();
            await trackDisplayService.LoadTrackCoverAsync(track, "high", true);

            if (track.Thumbnail != null)
            {
                CurrentTrackImage = track.Thumbnail;
            }
            else
            {
                CurrentTrackImage = null;
            }
        }

        private async Task UpdateLyricsForTrack(TrackDisplayModel track)
        {
            if (track == null)
            {
                TrackLyrics = _localizationService["NoTrackPlaying"];
                return;
            }

            TrackLyrics = !string.IsNullOrWhiteSpace(track.Lyrics)
                ? track.Lyrics
                : _localizationService["NoLyrics"];

            // Try to load high quality image
            var trackDisplayService = App.ServiceProvider.GetRequiredService<TrackDisplayService>();
            await trackDisplayService.LoadTrackCoverAsync(track, "high", true);

            if (track.Thumbnail != null)
            {
                CurrentTrackImage = track.Thumbnail;
            }
            else
            {
                CurrentTrackImage = null;
            }
        }
    }
}