using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OmegaPlayer.Core;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Shell.ViewModels;
using OmegaPlayer.UI.Helpers;

namespace OmegaPlayer.Features.Shell.Views
{
    public partial class MainView : Window
    {
        public MainView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);

            PropertyChanged += MainView_PropertyChanged;
        }

        private void MainView_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == WindowStateProperty)
            {
                if (this.FindControl<Grid>("CussstomTitleBar") is Grid titleBar)
                {
                    WindowProperties.SetIsWindowed(titleBar, WindowState != WindowState.Maximized);
                }
            }
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
                    if (DataContext is MainViewModel mainViewModel &&
                        mainViewModel.CurrentPage is ILoadMoreItems loadMoreItems &&
                        loadMoreItems.LoadMoreItemsCommand.CanExecute(null))
                    {
                        loadMoreItems.LoadMoreItemsCommand.Execute(null);
                    }
                }
            }
        }
        private void CussstomTitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleWindowState();
            }
            else if (e.ClickCount == 1)
            {
                BeginMoveDrag(e);
            }
        }
        private void ToggleWindowState()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        private void OnSortDirectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm && sender is ComboBox comboBox && comboBox.SelectedItem != null)
            {
                string direction = comboBox.SelectedItem.ToString();
                vm.SetSortDirection(direction);
            }
        }

        private void OnSortTypeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm && sender is ComboBox comboBox && comboBox.SelectedItem != null)
            {
                string sortType = comboBox.SelectedItem.ToString();
                vm.SetSortType(sortType);
            }
        }

    }
}