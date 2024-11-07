using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Input;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using OmegaPlayer.Core;
using OmegaPlayer.Features.Library.ViewModels;

namespace OmegaPlayer.Features.Library.Views
{

    public partial class GridView : UserControl
    {
        private readonly GridViewModel _viewModel;
        //private ScrollViewer _scrollViewer;

        public GridView()
        {
            InitializeComponent();

            ViewModelLocator.AutoWireViewModel(this);


        }


        private async Task LoadVisibleThumbnailsAsync()
        {
            var visibleTracks = GetVisibleTracks();

            if (visibleTracks != null && visibleTracks.Any() && DataContext is GridViewModel viewModel)
            {
                await viewModel.LoadHighResImagesForVisibleTracksAsync(visibleTracks);
            }
        }

        private IList<TrackDisplayModel> GetVisibleTracks()
        {
            var visibleTracks = new List<TrackDisplayModel>();

            foreach (var container in this.GetVisualChildren())
            {
                if (container is ContentControl contentControl && contentControl.IsVisible)
                {
                    var track = contentControl.DataContext as TrackDisplayModel;
                    if (track != null)
                    {
                        visibleTracks.Add(track);
                    }
                }
            }

            return visibleTracks;
        }


        private void Track_PointerEntered(object sender, PointerEventArgs e)
        {
            if (sender is StackPanel stackPanel && stackPanel.DataContext is TrackDisplayModel track)
            {
                track.IsPointerOver = true;
            }
        }

        private void Track_PointerExited(object sender, PointerEventArgs e)
        {
            if (sender is StackPanel stackPanel && stackPanel.DataContext is TrackDisplayModel track)
            {
                track.IsPointerOver = false;
            }
        }


    }
}