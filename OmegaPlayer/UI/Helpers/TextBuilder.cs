using Avalonia.Controls.Documents;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls.Documents;
using System.Text;
using System.Threading.Tasks;

namespace OmegaPlayer.UI.Controls.Helpers
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
            _parts.Add((text, decorations));
            return this;
        }

        public TextBuilder Substring(int start, int length)
        {
            if (_parts.Count > 0)
            {
                var text = _parts[0].Text.Substring(start, length);
                return new TextBuilder(text);
            }
            return this;
        }

        public InlineCollection ToFormattedString()
        {
            var inlines = new InlineCollection();
            foreach (var part in _parts)
            {
                var run = new Run(part.Text);
                if (part.Decorations != null)
                    run.TextDecorations = part.Decorations;
                inlines.Add(run);
            }
            return inlines;
        }
    }
}
