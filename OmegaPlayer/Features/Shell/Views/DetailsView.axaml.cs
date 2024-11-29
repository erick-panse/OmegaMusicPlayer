using Avalonia.Controls;
using OmegaPlayer.Core;
using OmegaPlayer.Features.Library.ViewModels;
using Avalonia;
using OmegaPlayer.Features.Shell.ViewModels;

namespace OmegaPlayer.Features.Shell.Views
{
    public partial class DetailsView : UserControl
    {
        private DetailsViewModel? ViewModel => DataContext as DetailsViewModel;

        public DetailsView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);
        }

        private void MainScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer && ViewModel != null)
            {
                ViewModel.OnScroll(scrollViewer.Offset.Y);

                // If the user scrolls near the end, trigger the load more command
                if (scrollViewer.Offset.Y >= scrollViewer.Extent.Height - scrollViewer.Viewport.Height - 100)
                {
                    if (ViewModel.LoadMoreItemsCommand.CanExecute(null))
                    {
                        ViewModel.LoadMoreItemsCommand.Execute(null);
                    }
                }
            }
        }
    }
}