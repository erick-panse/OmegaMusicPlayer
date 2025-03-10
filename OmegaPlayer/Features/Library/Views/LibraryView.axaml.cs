using Avalonia.Controls;
using OmegaPlayer.Core;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.ViewModels;

namespace OmegaPlayer.Features.Library.Views
{
    public partial class LibraryView : UserControl
    {
        public LibraryView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);
        }

        private void TrackControlScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender != null)
            {
                var scrollViewer = sender as ScrollViewer;

                // If the user scrolls near the end, trigger the load more command
                if (scrollViewer.Offset.Y >= scrollViewer.Extent.Height - scrollViewer.Viewport.Height - 100)
                {
                    // Get the current view's ViewModel
                    if (DataContext is LibraryViewModel libraryViewModel &&
                        libraryViewModel is ILoadMoreItems loadMoreItems &&
                        loadMoreItems.LoadMoreItemsCommand.CanExecute(null))
                    {
                        loadMoreItems.LoadMoreItemsCommand.Execute(null);
                    }
                }
            }
        }
    }
}