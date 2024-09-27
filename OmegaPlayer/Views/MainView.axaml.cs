using Avalonia;
using Avalonia.Controls;
using OmegaPlayer.ViewModels;

namespace OmegaPlayer.Views
{
    public partial class MainView : Window
    {
        public MainView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);
        }

        private void MainScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender != null)
            {
                var scrollViewer = sender as ScrollViewer;

                // If the user scrolls near the end, trigger the load more command
                if (scrollViewer.Offset.Y >= scrollViewer.Extent.Height - scrollViewer.Viewport.Height - 100)
                {
                    // Get the current view's ViewModel
                    if (DataContext is MainViewModel mainViewModel && mainViewModel.CurrentPage is ILoadMoreItems loadMoreItems)
                    {
                        // Call the load more items method defined in the ViewModel of the current view
                        loadMoreItems.LoadMoreItemsCommand.Execute(null);
                    }
                }

            }
        }
    }
}