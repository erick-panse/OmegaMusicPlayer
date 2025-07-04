using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Features.Playlists.ViewModels;
using OmegaPlayer.UI;
using System.Collections.Generic;

namespace OmegaPlayer.Features.Playlists.Views
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
            var playlistDisplayService = App.ServiceProvider.GetRequiredService<PlaylistDisplayService>();
            var errorHandlingService = App.ServiceProvider.GetRequiredService<IErrorHandlingService>();

            DataContext = new PlaylistSelectionDialogViewModel(
                this,
                playlistViewModel,
                selectedTracks,
                playlistDisplayService,
                errorHandlingService);
        }
    }
}