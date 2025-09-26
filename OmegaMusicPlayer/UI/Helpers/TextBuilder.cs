using Avalonia.Controls.Documents;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace OmegaMusicPlayer.UI.Controls.Helpers
{
    public class TextBuilder
    {
        private readonly List<(string Text, TextDecorationCollection? Decorations)> _parts = new();

        public TextBuilder(string initialText = "")
        {
            if (!string.IsNullOrEmpty(initialText))
                _parts.Add((initialText, null));
        }

        public TextBuilder Append(string text, TextDecorationCollection? decorations = null)
        {
            // Protect against null text
            _parts.Add((text ?? string.Empty, decorations));
            return this;
        }

        public TextBuilder Substring(int start, int length)
        {
            try
            {
                if (_parts.Count > 0)
                {
                    // Bounds checking to prevent exceptions
                    var text = _parts[0].Text;

                    // Adjust start to valid range
                    start = Math.Max(0, Math.Min(start, text.Length));

                    // Adjust length to not go beyond string bounds
                    length = Math.Max(0, Math.Min(length, text.Length - start));

                    // Extract substring safely
                    var substring = text.Substring(start, length);
                    return new TextBuilder(substring);
                }
                return new TextBuilder(); // Return empty builder if no parts
            }
            catch (Exception)
            {
                return new TextBuilder();
            }
        }

        public InlineCollection ToFormattedString()
        {
            try
            {
                var inlines = new InlineCollection();
                foreach (var part in _parts)
                {
                    var run = new Run(part.Text ?? string.Empty); // Protect against null text
                    if (part.Decorations != null)
                        run.TextDecorations = part.Decorations;
                    inlines.Add(run);
                }
                return inlines;
            }
            catch (Exception)
            {
                return new InlineCollection();
            }
        }
    }
}