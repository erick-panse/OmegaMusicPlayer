using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Playlists.Models;
using OmegaPlayer.Features.Playlists.Services;
using OmegaPlayer.Features.Playlists.ViewModels;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.UI;

namespace OmegaPlayer.Features.Playlists.Views
{
    public partial class PlaylistDialogView : Window
    {
        public PlaylistDialogView()
        {
            InitializeComponent();

        }

        public void Initialize(Playlist playlistToEdit = null)
        {
            var playlistService = App.ServiceProvider.GetRequiredService<PlaylistService>();
            var profileManager = App.ServiceProvider.GetRequiredService<ProfileManager>();
            var localizationService = App.ServiceProvider.GetRequiredService<LocalizationService>();
            var errorHandlingService = App.ServiceProvider.GetRequiredService<IErrorHandlingService>();
            
            DataContext = new PlaylistDialogViewModel(
                this,
                playlistService,
                profileManager,
                localizationService,
                errorHandlingService,
                playlistToEdit);
        }
    }
}