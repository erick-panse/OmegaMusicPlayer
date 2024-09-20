using Avalonia.Controls;
using OmegaPlayer.ViewModels;
using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Input;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace OmegaPlayer.Views
{

    public partial class GridView : UserControl
    {
        private readonly GridViewModel _viewModel;
        private ScrollViewer _scrollViewer;

        public GridView()
        {
            InitializeComponent();

            this.AttachedToVisualTree += GridView_AttachedToVisualTree;
        }

        private void GridView_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            _scrollViewer = this.FindControl<ScrollViewer>("GridScrollViewer");

            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            }
        }


        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_scrollViewer != null)
            {
                var verticalOffset = _scrollViewer.Offset.Y;
                var scrollableHeight = _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height;

                // Inform the ViewModel of scroll position changes
                if (DataContext is GridViewModel viewModel)
                {
                    viewModel.OnScrollChanged(verticalOffset, scrollableHeight);
                }
            }
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


    }
}