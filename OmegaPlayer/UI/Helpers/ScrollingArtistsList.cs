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
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

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

        public static readonly StyledProperty<bool> ShowUnderlineProperty =
            AvaloniaProperty.Register<ScrollingArtistsList, bool>(
                nameof(ShowUnderlineProperty), true);
        public bool ShowUnderline
        {
            get => GetValue(ShowUnderlineProperty);
            set => SetValue(ShowUnderlineProperty, value);
        }

        // new property for AnimationWidth
        public static readonly StyledProperty<double> AnimationWidthProperty =
            AvaloniaProperty.Register<ScrollingArtistsList, double>(
                nameof(AnimationWidth),
                double.NaN); // Default to NaN to indicate "not set"

        public double AnimationWidth
        {
            get => GetValue(AnimationWidthProperty);
            set => SetValue(AnimationWidthProperty, value);
        }

        public static readonly StyledProperty<double> FontSizeProperty =
            AvaloniaProperty.Register<ScrollingArtistsList, double>(
                nameof(FontSize), 12); // Default to 12

        public double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        private CustomTextBlock _textBlock;
        private string _fullText;
        private List<(int Start, int End, Artists Artist)> _artistPositions;
        private int? _hoveredArtistIndex;
        private bool _isDisposed = false;
        private IDisposable _propertyChangedSubscription;
        private IErrorHandlingService _errorHandlingService;

        public ScrollingArtistsList()
        {
            // Get error handling service if available
            _errorHandlingService = App.ServiceProvider?.GetService<IErrorHandlingService>();
            _artistPositions = new List<(int, int, Artists)>();
            _fullText = string.Empty;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            try
            {
                base.OnApplyTemplate(e);

                // Unsubscribe from previous events if existing
                if (_textBlock != null)
                {
                    _textBlock.PointerPressed -= TextBlock_PointerPressed;
                    _textBlock.PointerMoved -= TextBlock_PointerMoved;
                    _textBlock.PointerExited -= TextBlock_PointerExited;
                }

                // Unregister previous property change subscription
                _propertyChangedSubscription?.Dispose();

                // Find the text block in the template
                _textBlock = e.NameScope.Find<CustomTextBlock>("PART_TextBlock");

                if (_textBlock != null)
                {
                    // Pass the AnimationWidth to the CustomTextBlock
                    _textBlock.AnimationWidth = AnimationWidth;
                    _textBlock.FontSize = FontSize;

                    // Setup a binding to update the CustomTextBlock's AnimationWidth when this control's AnimationWidth changes
                    this.GetObservable(AnimationWidthProperty).Subscribe(width =>
                    {
                        if (_textBlock != null && !_isDisposed)
                        {
                            _textBlock.AnimationWidth = width;
                        }
                    });

                    // Add event handlers
                    _textBlock.PointerPressed += TextBlock_PointerPressed;
                    _textBlock.PointerMoved += TextBlock_PointerMoved;
                    _textBlock.PointerExited += TextBlock_PointerExited;

                    // Initialize text content
                    UpdateText(Artists);

                    // Subscribe to property changes
                    _propertyChangedSubscription = Observable.FromEventPattern<AvaloniaPropertyChangedEventArgs>(
                        h => this.PropertyChanged += h,
                        h => this.PropertyChanged -= h)
                        .Where(x => x.EventArgs.Property == ArtistsProperty && !_isDisposed)
                        .Subscribe(x => UpdateText(Artists));
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error applying template to ScrollingArtistsList",
                    ex.Message,
                    ex,
                    false);
            }
        }

        private void UpdateText(IList<Artists> artists)
        {
            if (_textBlock == null || artists == null || _isDisposed) return;

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
            try
            {
                if (_isDisposed || _textBlock == null) return;

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
                                inlines.Add(new Run(_fullText.Substring(lastEnd, pos.Start - lastEnd)));
                            }

                            // Add the artist name
                            var artistRun = new Run(_fullText.Substring(pos.Start, pos.End - pos.Start));
                            if (i == index && ShowUnderline == true)
                            {
                                artistRun.TextDecorations = TextDecorations.Underline;
                            }
                            else
                            {
                                if (i == _artistPositions.Count - 1 && index == i - 1 && ShowUnderline == true)
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
                                if (index == i && ShowUnderline == true)
                                {
                                    // If we're hovering over this artist, include the comma in the styling
                                    inlines.Add(new Run()
                                    {
                                        TextDecorations = TextDecorations.Underline,
                                    });
                                    inlines.Add(new Run(", "));
                                }
                                else
                                {
                                    inlines.Add(new Run(","));
                                    inlines.Add(new Run(" "));
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
                    }
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error updating hovered artist in ScrollingArtistsList",
                    ex.Message,
                    ex,
                    false);

                // Try to reset to basic state on error
                if (_textBlock != null && !_isDisposed)
                {
                    _textBlock.Inlines = null;
                    _textBlock.Text = _fullText;
                }
            }
        }

        private void TextBlock_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (_isDisposed || ArtistClickCommand == null || _artistPositions == null) return;

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
            if (_isDisposed) return;

            var position = e.GetPosition(_textBlock);
            var textPosition = GetCharacterIndexFromPoint(position);

            int hoveredIndex = _artistPositions.FindIndex(p =>
                textPosition >= p.Start && textPosition < p.End);

            UpdateHoveredArtist(hoveredIndex);
        }

        private void TextBlock_PointerExited(object sender, PointerEventArgs e)
        {
            if (_isDisposed) return;

            UpdateHoveredArtist(-1);
        }

        private int GetCharacterIndexFromPoint(Point point)
        {
            try
            {
                if (_textBlock == null || _fullText == null || _isDisposed)
                    return 0;

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
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error calculating character index from point",
                    ex.Message,
                    ex,
                    false);
                return 0;
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            // Mark as disposed to prevent further operations
            _isDisposed = true;

            // Unsubscribe from events
            if (_textBlock != null)
            {
                _textBlock.PointerPressed -= TextBlock_PointerPressed;
                _textBlock.PointerMoved -= TextBlock_PointerMoved;
                _textBlock.PointerExited -= TextBlock_PointerExited;
            }

            // Dispose of property change subscription
            _propertyChangedSubscription?.Dispose();

            // Clear references
            _textBlock = null;
            _artistPositions?.Clear();
            _fullText = null;

            base.OnDetachedFromVisualTree(e);
        }
    }
}