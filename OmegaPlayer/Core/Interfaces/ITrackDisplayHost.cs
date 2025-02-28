using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.UI;

namespace OmegaPlayer.Core.Interfaces
{
    /// <summary>
    /// Defines the interface for view models that host track displays
    /// This allows binding to commands and properties from multiple view model types
    /// </summary>
    public interface ITrackDisplayHost
    {
        // Properties
        ViewType CurrentViewType { get; }
        ObservableCollection<TrackDisplayModel> Tracks { get; }
        bool IsReorderMode { get; }
        TrackDisplayModel DraggedTrack { get; set; }

        // Commands
        ICommand TrackSelectionCommand { get; }
        ICommand PlayTrackCommand { get; }
        ICommand OpenArtistCommand { get; }
        ICommand OpenAlbumCommand { get; }
        ICommand OpenGenreCommand { get; }
        ICommand ToggleTrackLikeCommand { get; }
        ICommand AddToQueueCommand { get; }
        ICommand AddAsNextTracksCommand { get; }
        ICommand ShowPlaylistSelectionDialogCommand { get; }
        ICommand RemoveTracksFromPlaylistCommand { get; }

        // Drag and Drop handling methods
        void HandleTrackDragStarted(TrackDisplayModel track);
        void HandleTrackDragOver(int newIndex);
        void HandleTrackDrop();
    }
}