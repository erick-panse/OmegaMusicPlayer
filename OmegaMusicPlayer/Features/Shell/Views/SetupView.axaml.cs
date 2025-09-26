using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using OmegaMusicPlayer.Features.Shell.ViewModels;
using System;

namespace OmegaMusicPlayer.Features.Shell.Views
{
    public partial class SetupView : Window
    {
        public bool SetupCompleted { get; private set; } = false;
        private double _savedScrollPosition = 0;
        private ScrollViewer _scrollViewer;

        public SetupView()
        {
            InitializeComponent();

            // Set up the window properties
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.CanResize = false;
            this.ShowInTaskbar = false;

            // Initialize the view model
            DataContext = new SetupViewModel(this);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            // If setup was completed successfully, mark it as completed
            if (DataContext is SetupViewModel vm)
            {
                if (vm.CurrentStep == SetupViewModel.SetupStep.Completed)
                {
                    SetupCompleted = true;
                }

                // Dispose the view model to clean up resources
                vm.Dispose();
            }

            base.OnClosing(e);
        }

        // Event handlers for CustomComboBox controls (similar to ConfigView)
        private void ComboBox_GotFocus(object sender, GotFocusEventArgs e)
        {
            // Save current position if we had a scroll viewer
            _savedScrollPosition = 0;
        }

        private void ComboBox_DropDownOpened(object sender, EventArgs e)
        {
            // No scroll viewer manipulation needed for setup view since it's fixed size
            // But keeping the same pattern as ConfigView for consistency
        }

        private void ComboBox_DropDownClosed(object sender, EventArgs e)
        {
            // No scroll viewer manipulation needed for setup view
            // But keeping the same pattern as ConfigView for consistency
        }
    }
}