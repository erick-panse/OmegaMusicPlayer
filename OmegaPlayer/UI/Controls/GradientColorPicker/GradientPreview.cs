using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using System;

namespace OmegaPlayer.UI.Controls
{
    public class GradientPreview : Control
    {
        public static readonly StyledProperty<IBrush> BorderBrushProperty =
            AvaloniaProperty.Register<GradientPreview, IBrush>(nameof(BorderBrush));

        public static readonly StyledProperty<double> BorderThicknessProperty =
            AvaloniaProperty.Register<GradientPreview, double>(nameof(BorderThickness));

        public static readonly StyledProperty<double> CornerRadiusProperty =
            AvaloniaProperty.Register<GradientPreview, double>(nameof(CornerRadius), 4.0);

        public static readonly StyledProperty<string> StartColorProperty =
            AvaloniaProperty.Register<GradientPreview, string>(nameof(StartColor));

        public static readonly StyledProperty<string> EndColorProperty =
            AvaloniaProperty.Register<GradientPreview, string>(nameof(EndColor));

        static GradientPreview()
        {
            StartColorProperty.Changed.Subscribe(OnColorChanged);
            EndColorProperty.Changed.Subscribe(OnColorChanged);
        }

        private static void OnColorChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Sender is GradientPreview preview)
            {
                preview.InvalidateVisual();
            }
        }

        public IBrush BorderBrush
        {
            get => GetValue(BorderBrushProperty);
            set => SetValue(BorderBrushProperty, value);
        }

        public double BorderThickness
        {
            get => GetValue(BorderThicknessProperty);
            set => SetValue(BorderThicknessProperty, value);
        }

        public double CornerRadius
        {
            get => GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public string StartColor
        {
            get => GetValue(StartColorProperty);
            set => SetValue(StartColorProperty, value);
        }

        public string EndColor
        {
            get => GetValue(EndColorProperty);
            set => SetValue(EndColorProperty, value);
        }

        private Color ParseColorString(string colorStr)
        {
            if (string.IsNullOrEmpty(colorStr)) return Colors.Transparent;

            try
            {
                // Handle hex color strings
                if (!colorStr.StartsWith("#"))
                {
                    colorStr = "#" + colorStr;
                }
                return Color.Parse(colorStr);
            }
            catch (Exception ex)
            {
                return Colors.Transparent;
            }
        }

        public override void Render(DrawingContext context)
        {
            var rect = new Rect(0, 0, Bounds.Width, Bounds.Height);
            var cornerRadius = new CornerRadius(CornerRadius);

            // Parse colors and log
            var startColorParsed = ParseColorString(StartColor);
            var endColorParsed = ParseColorString(EndColor);

            // Create gradient brush
            var gradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative)
            };

            gradient.GradientStops.Add(new GradientStop(startColorParsed, 0));
            gradient.GradientStops.Add(new GradientStop(endColorParsed, 1));

            // Draw border if brush and thickness are set
            if (BorderBrush != null && BorderThickness > 0)
            {
                context.DrawRectangle(
                    null,
                    new Pen(BorderBrush, BorderThickness),
                    new RoundedRect(rect, cornerRadius.TopLeft, cornerRadius.TopRight, cornerRadius.BottomRight, cornerRadius.BottomLeft)
                );
            }

            // Draw gradient
            context.DrawRectangle(
                gradient,
                null,
                new RoundedRect(rect, cornerRadius.TopLeft, cornerRadius.TopRight, cornerRadius.BottomRight, cornerRadius.BottomLeft)
            );
        }
    }
}