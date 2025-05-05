using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Shell.ViewModels;
using OmegaPlayer.UI;
using OmegaPlayer.UI.Helpers;
using System;

namespace OmegaPlayer.Features.Shell.Views
{
    public partial class MainView : Window
    {
        private IErrorHandlingService _errorHandlingService;

        public MainView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);

            _errorHandlingService = App.ServiceProvider.GetRequiredService<IErrorHandlingService>();

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

        private void CussstomTitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    if (e.ClickCount == 2)
                    {
                        ToggleWindowState();
                    }
                    else if (e.ClickCount == 1)
                    {
                        BeginMoveDrag(e);
                    }
                },
                "Processing title bar interaction",
                ErrorSeverity.NonCritical,
                false
            );
        }

        private void ToggleWindowState()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void OnSortDropdownButtonClick(object sender, RoutedEventArgs e)
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    // Find the popup
                    var popup = this.FindControl<Popup>("SortPopup");
                    if (popup != null)
                    {
                        // Toggle the popup visibility
                        popup.IsOpen = !popup.IsOpen;

                        // Initialize temp values when opening
                        if (popup.IsOpen && DataContext is MainViewModel vm)
                        {
                            vm.InitializeTempSortSettings();
                        }
                    }
                },
                "Opening sort dropdown",
                ErrorSeverity.NonCritical,
                false
            );
        }

        private void OnApplyButtonClick(object sender, RoutedEventArgs e)
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    // Close the popup directly from the button click
                    var popup = this.FindControl<Popup>("SortPopup");
                    if (popup != null)
                    {
                        popup.IsOpen = false;
                    }

                    // Let the event bubble to run the command
                    e.Handled = false;
                },
                "Applying sort settings",
                ErrorSeverity.NonCritical,
                false
            );
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                // Only trigger search if there's text
                if (!string.IsNullOrWhiteSpace(vm.SearchViewModel.SearchQuery))
                {
                    vm.SearchViewModel.ShowSearchFlyout = true;
                    vm.SearchViewModel.SearchPreviewCommand.Execute(null);
                }
                else
                {
                    vm.SearchViewModel.ShowSearchFlyout = false;
                }
            }
        }

        private void OnSearchPopupClosed(object sender, EventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                // Don't reset search query when popup closes
                // Only update the flyout state
                vm.SearchViewModel.ShowSearchFlyout = false;
            }
        }

        private void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    if (e.Key == Key.Enter && DataContext is MainViewModel vm)
                    {
                        vm.SearchViewModel.SearchCommand.Execute(null);
                    }
                },
                "Processing search keyboard input",
                ErrorSeverity.NonCritical,
                false
            );
        }
    }
}