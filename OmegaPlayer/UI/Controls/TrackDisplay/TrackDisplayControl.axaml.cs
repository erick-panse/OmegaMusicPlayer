using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.ViewModels;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;

namespace OmegaPlayer.UI.Controls.TrackDisplay
{
    public partial class TrackDisplayControl : TemplatedControl
    {
        public static readonly StyledProperty<ViewType> ViewTypeProperty =
            AvaloniaProperty.Register<TrackDisplayControl, ViewType>(
                nameof(ViewType),
                ViewType.Card);

        public ViewType ViewType
        {
            get => GetValue(ViewTypeProperty);
            set => SetValue(ViewTypeProperty, value);
        }

        public static readonly StyledProperty<System.Collections.IEnumerable> ItemsSourceProperty =
            AvaloniaProperty.Register<TrackDisplayControl, System.Collections.IEnumerable>(
                nameof(ItemsSource));

        public System.Collections.IEnumerable ItemsSource
        {
            get => GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        private ItemsControl _itemsControl;

        public TrackDisplayControl()
        {
            InitializeComponent();
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            if (e.NameScope.Find<ItemsControl>("PART_ItemsControl") is ItemsControl itemsControl)
            {
                _itemsControl = itemsControl;
                itemsControl.AddHandler(InputElement.PointerReleasedEvent, ArtistClicked, RoutingStrategies.Tunnel);
                itemsControl.AddHandler(InputElement.PointerEnteredEvent, Track_PointerEntered, RoutingStrategies.Tunnel);
                itemsControl.AddHandler(InputElement.PointerExitedEvent, Track_PointerExited, RoutingStrategies.Tunnel);
            }
        }

        private void ArtistClicked(object? sender, PointerReleasedEventArgs e)
        {
            if (e.Source is TextBlock textBlock &&
                textBlock.Name == "ArtistTextBlock" &&
                textBlock.Tag is Artists artist &&
                DataContext is LibraryViewModel viewModel)
            {
                viewModel.OpenArtistCommand.Execute(artist);
                e.Handled = true;
            }
        }

        private void Track_PointerEntered(object? sender, PointerEventArgs e)
        {
            if (e.Source is StackPanel stackPanel &&
                stackPanel.Name == "TrackPanel" &&
                stackPanel.DataContext is TrackDisplayModel track)
            {
                track.IsPointerOver = true;
            }
        }

        private void Track_PointerExited(object? sender, PointerEventArgs e)
        {
            if (e.Source is StackPanel stackPanel &&
                stackPanel.Name == "TrackPanel" &&
                stackPanel.DataContext is TrackDisplayModel track)
            {
                track.IsPointerOver = false;
            }
        }



        private async void ShowMessageBox(string message)
        {
            var messageBox = MessageBoxManager.GetMessageBoxStandard("Debug", message, ButtonEnum.Ok, Icon.Info);
            await messageBox.ShowWindowAsync();
        }
    }
}