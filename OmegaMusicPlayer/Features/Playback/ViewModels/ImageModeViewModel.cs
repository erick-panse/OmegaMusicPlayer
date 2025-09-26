using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Core.Messages;
using OmegaMusicPlayer.Core.ViewModels;
using OmegaMusicPlayer.Features.Library.Models;
using OmegaMusicPlayer.Features.Library.Services;
using OmegaMusicPlayer.UI;
using System.Threading.Tasks;

namespace OmegaMusicPlayer.Features.Playback.ViewModels
{
    public partial class ImageModeViewModel : ViewModelBase
    {
        private readonly TrackControlViewModel _trackControlViewModel;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private Bitmap _currentTrackImage;

        [ObservableProperty]
        private bool _hasTrackImage;

        [ObservableProperty]
        private bool _isClosing;

        public ImageModeViewModel(
            TrackControlViewModel trackControlViewModel,
            IErrorHandlingService errorHandlingService,
            IMessenger messenger)
        {
            _trackControlViewModel = trackControlViewModel;
            _errorHandlingService = errorHandlingService;
            _messenger = messenger;

            // Subscribe to track changes
            _messenger.Register<CurrentTrackChangedMessage>(this, (r, m) =>
            {
                _ = UpdateTrackInfo(m.CurrentTrack);
            });
        }

        public void Initialize()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    var currentTrack = _trackControlViewModel.CurrentlyPlayingTrack;
                    if (currentTrack != null)
                    {
                        _ = UpdateTrackInfo(currentTrack);
                    }
                },
                "Initializing image mode",
                ErrorSeverity.NonCritical,
                false
            );
        }

        private async Task UpdateTrackInfo(TrackDisplayModel track)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (track == null) return;

                    // Try to load high quality image
                    var trackDisplayService = App.ServiceProvider.GetRequiredService<TrackDisplayService>();
                    await trackDisplayService.LoadTrackCoverAsync(track, "detail", true);

                    if (track.Thumbnail != null)
                    {
                        CurrentTrackImage = null;
                        CurrentTrackImage = track.Thumbnail;
                        HasTrackImage = true;
                    }
                    else
                    {
                        CurrentTrackImage = null;
                        HasTrackImage = false;
                    }
                },
                "Updating track information in image mode",
                ErrorSeverity.NonCritical,
                false
            );
        }

        [RelayCommand]
        public void CloseImageMode()
        {
            // Navigate to image mode (will close image mode)
            _messenger.Send(new ShowImageModeMessage());
        }
    }
}
