using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Features.Library.Models;
using OmegaMusicPlayer.Features.Library.Services;
using OmegaMusicPlayer.Features.Library.ViewModels;
using OmegaMusicPlayer.Features.Playlists.ViewModels;
using OmegaMusicPlayer.Infrastructure.Services;
using OmegaMusicPlayer.UI;
using System.Collections.Generic;

namespace OmegaMusicPlayer.Features.Playlists.Views
{
    public partial class PlaylistSelectionDialog : Window
    {
        public PlaylistSelectionDialog()
        {
            InitializeComponent();
        }

        public void Initialize(
        PlaylistsViewModel playlistViewModel,
        LibraryViewModel libraryViewModel,
        IEnumerable<TrackDisplayModel> selectedTracks)
        {
            var localizationService = App.ServiceProvider.GetRequiredService<LocalizationService>();
            var playlistDisplayService = App.ServiceProvider.GetRequiredService<PlaylistDisplayService>();
            var errorHandlingService = App.ServiceProvider.GetRequiredService<IErrorHandlingService>();

            DataContext = new PlaylistSelectionDialogViewModel(
                this,
                playlistViewModel,
                localizationService,
                selectedTracks,
                playlistDisplayService,
                errorHandlingService);
        }
    }
}