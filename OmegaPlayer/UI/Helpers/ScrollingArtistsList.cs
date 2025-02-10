using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using System.Collections.Generic;
using System.Text;
using OmegaPlayer.Features.Library.Models;
using System.Linq;
using Avalonia.Controls.Documents;
using System.Windows.Input;
using System;
using System.Reactive.Linq;

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
        private int? _hoveredArtistIndex;

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _textBlock = e.NameScope.Find<CustomTextBlock>("PART_TextBlock");

            if (_textBlock != null)
            {
                _textBlock.PointerPressed += TextBlock_PointerPressed;
                _textBlock.PointerMoved += TextBlock_PointerMoved;
                _textBlock.PointerExited += TextBlock_PointerExited;
                UpdateText(Artists);

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
            var textBuilder = new StringBuilder();
            var currentPosition = 0;

            var validArtists = artists.Where(a => !string.IsNullOrEmpty(a.ArtistName)).ToList();
            for (int i = 0; i < validArtists.Count; i++)
            {
                var artist = validArtists[i];

                if (currentPosition > 0)
                {
                    textBuilder.Append(",");
                    currentPosition += 1; // Account for comma
                    textBuilder.Append(" ");
                    currentPosition += 1; // Account for space separately
                }

                int startPosition = currentPosition;
                textBuilder.Append(artist.ArtistName);
                currentPosition += artist.ArtistName.Length;

                _artistPositions.Add((startPosition, currentPosition, artist));
            }

            _fullText = textBuilder.ToString();
            _textBlock.Text = _fullText;
            UpdateHoveredArtist(-1); // Reset hover state
        }

        private void UpdateHoveredArtist(int index)
        {
            if (_hoveredArtistIndex != index)
            {
                _hoveredArtistIndex = index;

                if (index >= 0 && index < _artistPositions.Count)
                {
                    var inlines = new InlineCollection();
                    int lastEnd = 0;

                    // Process each artist section
                    for (int i = 0; i < _artistPositions.Count; i++)
                    {
                        var pos = _artistPositions[i];

                        // Add text before this artist (comma or initial text)
                        if (pos.Start > lastEnd)
                        {
                            inlines.Add(new Run(_fullText.Substring(lastEnd, pos.Start - lastEnd))
                            {
                                Foreground = this.FindResource("TextColor") as IBrush ?? Brushes.White
                            });
                        }

                        // Add the artist name
                        var artistRun = new Run(_fullText.Substring(pos.Start, pos.End - pos.Start));
                        if (i == index)
                        {
                            artistRun.TextDecorations = TextDecorations.Underline;
                            artistRun.Foreground = this.FindResource("AccentColor") as IBrush ?? Brushes.White;
                        }
                        else
                        {
                            artistRun.Foreground = this.FindResource("TextColor") as IBrush ?? Brushes.White;
                            if (i == _artistPositions.Count - 1 && index == i - 1)
                            {
                                // If this is the last artist and we're hovering over the previous one,
                                // we need to include the comma in the underline
                                artistRun.TextDecorations = TextDecorations.Underline;
                            }
                        }
                        inlines.Add(artistRun);

                        // Add comma and space after artist if it's not the last one
                        if (i < _artistPositions.Count - 1)
                        {
                            if (index == i)
                            {
                                // If we're hovering over this artist, include the comma in the styling
                                inlines.Add(new Run()
                                {
                                    TextDecorations = TextDecorations.Underline,
                                    Foreground = this.FindResource("AccentColor") as IBrush ?? Brushes.White
                                });
                                inlines.Add(new Run(", ")
                                {
                                    Foreground = this.FindResource("TextColor") as IBrush ?? Brushes.White
                                });
                            }
                            else
                            {
                                inlines.Add(new Run(",")
                                {
                                    Foreground = this.FindResource("TextColor") as IBrush ?? Brushes.White
                                });
                                inlines.Add(new Run(" ")
                                {
                                    Foreground = this.FindResource("TextColor") as IBrush ?? Brushes.White
                                });
                            }
                        }

                        lastEnd = pos.End + (i < _artistPositions.Count - 1 ? 1 : 0); // Account for comma first
                        if (i < _artistPositions.Count - 1)
                        {
                            lastEnd += 1; // Then account for space
                        }

                    }

                    _textBlock.Inlines = inlines;
                }
                else
                {
                    // Reset to default state
                    _textBlock.Inlines = null;
                    _textBlock.Text = _fullText;
                    _textBlock.Foreground = this.FindResource("TextColor") as IBrush ?? Brushes.White;
                }
            }
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

        private void TextBlock_PointerMoved(object sender, PointerEventArgs e)
        {
            var position = e.GetPosition(_textBlock);
            var textPosition = GetCharacterIndexFromPoint(position);

            int hoveredIndex = _artistPositions.FindIndex(p =>
                textPosition >= p.Start && textPosition < p.End);

            UpdateHoveredArtist(hoveredIndex);
        }

        private void TextBlock_PointerExited(object sender, PointerEventArgs e)
        {
            UpdateHoveredArtist(-1);
        }

        private int GetCharacterIndexFromPoint(Point point)
        {
            double approximateCharWidth = _textBlock.FontSize * 0.2; // Approximate character width
            double xOffset = 0;

            // Adjust for text alignment
            if (TextAlignment == TextAlignment.Center)
            {
                double totalWidth = _fullText.Length * approximateCharWidth;
                xOffset = (_textBlock.Bounds.Width - totalWidth) / 2;
            }

            int charIndex = (int)((point.X - xOffset) / approximateCharWidth);
            return Math.Max(0, Math.Min(charIndex, _fullText.Length - 1));
        }
    }
}