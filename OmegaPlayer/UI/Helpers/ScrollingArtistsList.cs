using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.UI.Controls.Helpers;
using System.Linq;
using System.Windows.Input;
using System;
using System.Reactive.Linq;
using Avalonia.Media;

namespace OmegaPlayer.UI.Controls.Helpers
{
    public class ScrollingArtistsList : TemplatedControl
    {
        public static readonly StyledProperty<IList<Artists>> ArtistsProperty =
            AvaloniaProperty.Register<ScrollingArtistsList, IList<Artists>>(
                nameof(Artists));

        public IList<Artists> Artists
        {
            get => GetValue(ArtistsProperty);
            set => SetValue(ArtistsProperty, value);
        }

        public static readonly StyledProperty<ICommand> ArtistClickCommandProperty =
            AvaloniaProperty.Register<ScrollingArtistsList, ICommand>(
                nameof(ArtistClickCommand));

        public ICommand ArtistClickCommand
        {
            get => GetValue(ArtistClickCommandProperty);
            set => SetValue(ArtistClickCommandProperty, value);
        }

        public static readonly StyledProperty<TextAlignment> TextAlignmentProperty =
            AvaloniaProperty.Register<ScrollingArtistsList, TextAlignment>(
                nameof(TextAlignment),
                TextAlignment.Center);

        public TextAlignment TextAlignment
        {
            get => GetValue(TextAlignmentProperty);
            set => SetValue(TextAlignmentProperty, value);
        }

        private CustomTextBlock _textBlock;
        private string _fullText;
        private List<(int Start, int End, Artists Artist)> _artistPositions;

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _textBlock = e.NameScope.Find<CustomTextBlock>("PART_TextBlock");

            if (_textBlock != null)
            {
                _textBlock.PointerPressed += TextBlock_PointerPressed;
                UpdateText(Artists);  // Initial update with current value
                Observable.FromEventPattern<AvaloniaPropertyChangedEventArgs>(
                    h => this.PropertyChanged += h,
                    h => this.PropertyChanged -= h)
                    .Where(x => x.EventArgs.Property == ArtistsProperty)
                    .Subscribe(x => UpdateText(Artists));
            }
        }

        private void UpdateText(IList<Artists> artists)
        {
            if (_textBlock == null || artists == null) return;

            _artistPositions = new List<(int, int, Artists)>();
            var currentPosition = 0;
            var textParts = new List<string>();

            foreach (var artist in artists)
            {
                if (currentPosition > 0)
                {
                    textParts.Add(", ");
                    currentPosition += 2;
                }

                _artistPositions.Add((currentPosition, currentPosition + artist.ArtistName.Length, artist));
                textParts.Add(artist.ArtistName);
                currentPosition += artist.ArtistName.Length;
            }

            _fullText = string.Concat(textParts);
            _textBlock.Text = _fullText;
        }

        private void TextBlock_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (ArtistClickCommand == null || _artistPositions == null) return;

            var position = e.GetPosition(_textBlock);
            var textPosition = GetCharacterIndexFromPoint(position);

            var clickedArtist = _artistPositions.FirstOrDefault(p =>
                textPosition >= p.Start && textPosition < p.End);

            if (clickedArtist != default)
            {
                ArtistClickCommand.Execute(clickedArtist.Artist);
            }
        }

        private int GetCharacterIndexFromPoint(Point point)
        {
            // This is a simplified calculation. You might need to adjust it based on your font metrics
            return (int)(point.X / (_textBlock.FontSize * 0.6));
        }
    }
}