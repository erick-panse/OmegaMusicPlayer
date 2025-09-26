using Avalonia.Media;
using System;

namespace OmegaMusicPlayer.Core.Utils
{
    /// <summary>
    /// Provides utility methods for color manipulation in the theme system.
    /// </summary>
    public static class ColorUtilities
    {
        /// <summary>
        /// Creates a darker shade of the provided color.
        /// </summary>
        /// <param name="color">The base color to darken</param>
        /// <param name="factor">The darkening factor (0.0 to 1.0, where 0.0 means no change and 1.0 means completely black)</param>
        /// <returns>The darkened color</returns>
        public static Color Darken(this Color color, double factor)
        {
            factor = Math.Clamp(factor, 0, 1);

            byte r = (byte)(color.R * (1 - factor));
            byte g = (byte)(color.G * (1 - factor));
            byte b = (byte)(color.B * (1 - factor));

            return new Color(color.A, r, g, b);
        }

        /// <summary>
        /// Creates a lighter shade of the provided color using HSL color space.
        /// This preserves the hue and saturation while only increasing lightness.
        /// </summary>
        /// <param name="color">The base color to lighten</param>
        /// <param name="factor">The lightening factor (0.0 to 1.0, where 0.0 means no change and 1.0 means maximum lightness increase)</param>
        /// <param name="preserveSaturation">Whether to preserve saturation (true) or allow some desaturation (false)</param>
        /// <returns>The lightened color</returns>
        public static Color Lighten(this Color color, double factor, bool preserveSaturation = true)
        {
            factor = Math.Clamp(factor, 0, 1);

            // Convert RGB to HSL
            var (h, s, l) = RgbToHsl(color);

            // Adjust lightness while preserving hue
            // The formula limits maximum lightness to avoid going full white
            l = Math.Min(0.9, l + (0.9 - l) * factor);

            // Optionally reduce saturation slightly for more natural lightening
            if (!preserveSaturation)
            {
                s = Math.Max(0, s - (s * factor * 0.3));
            }

            // Convert back to RGB
            return HslToRgb(h, s, l);
        }

        /// <summary>
        /// Converts RGB color to HSL (Hue, Saturation, Lightness) representation.
        /// </summary>
        private static (double h, double s, double l) RgbToHsl(Color color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(Math.Max(r, g), b);
            double min = Math.Min(Math.Min(r, g), b);
            double delta = max - min;

            double h = 0;
            double s = 0;
            double l = (max + min) / 2;

            if (delta != 0)
            {
                s = l < 0.5 ? delta / (max + min) : delta / (2 - max - min);

                if (r == max)
                {
                    h = (g - b) / delta + (g < b ? 6 : 0);
                }
                else if (g == max)
                {
                    h = (b - r) / delta + 2;
                }
                else
                {
                    h = (r - g) / delta + 4;
                }

                h /= 6;
            }

            return (h, s, l);
        }

        /// <summary>
        /// Converts HSL (Hue, Saturation, Lightness) to RGB color.
        /// </summary>
        private static Color HslToRgb(double h, double s, double l)
        {
            if (s == 0)
            {
                // Achromatic (gray)
                byte value = (byte)(l * 255);
                return new Color(255, value, value, value);
            }

            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p = 2 * l - q;

            double rt = HueToRgb(p, q, h + 1.0 / 3);
            double gt = HueToRgb(p, q, h);
            double bt = HueToRgb(p, q, h - 1.0 / 3);

            return new Color(
                255,
                (byte)(rt * 255),
                (byte)(gt * 255),
                (byte)(bt * 255)
            );
        }

        /// <summary>
        /// Helper function for HSL to RGB conversion.
        /// </summary>
        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;

            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;

            return p;
        }

        /// <summary>
        /// Creates a LinearGradientBrush with lighter versions of the gradient stops.
        /// </summary>
        /// <param name="brush">The base gradient brush</param>
        /// <param name="factor">The lightening factor</param>
        /// <returns>A new gradient brush with lightened colors</returns>
        public static LinearGradientBrush Lighten(this LinearGradientBrush brush, double factor)
        {
            var newBrush = new LinearGradientBrush
            {
                StartPoint = brush.StartPoint,
                EndPoint = brush.EndPoint
            };

            foreach (var stop in brush.GradientStops)
            {
                newBrush.GradientStops.Add(new GradientStop(stop.Color.Lighten(factor), stop.Offset));
            }

            return newBrush;
        }

        /// <summary>
        /// Creates a LinearGradientBrush with darker versions of the gradient stops.
        /// </summary>
        /// <param name="brush">The base gradient brush</param>
        /// <param name="factor">The darkening factor</param>
        /// <returns>A new gradient brush with darkened colors</returns>
        public static LinearGradientBrush Darken(this LinearGradientBrush brush, double factor)
        {
            var newBrush = new LinearGradientBrush
            {
                StartPoint = brush.StartPoint,
                EndPoint = brush.EndPoint
            };

            foreach (var stop in brush.GradientStops)
            {
                newBrush.GradientStops.Add(new GradientStop(stop.Color.Darken(factor), stop.Offset));
            }

            return newBrush;
        }

        /// <summary>
        /// Gets the average color of a linear gradient brush.
        /// Useful for creating solid color variants from gradients.
        /// </summary>
        /// <param name="brush">The gradient brush</param>
        /// <returns>An average solid color from the gradient</returns>
        public static Color GetAverageColor(this LinearGradientBrush brush)
        {
            if (brush.GradientStops.Count == 0)
                return Colors.Transparent;

            if (brush.GradientStops.Count == 1)
                return brush.GradientStops[0].Color;

            double r = 0, g = 0, b = 0, a = 0;
            double totalWeight = 0;

            // Calculate weighted average based on stop positions
            for (int i = 0; i < brush.GradientStops.Count - 1; i++)
            {
                var stop1 = brush.GradientStops[i];
                var stop2 = brush.GradientStops[i + 1];
                double segmentWeight = stop2.Offset - stop1.Offset;

                // Add contribution from this segment
                r += ((stop1.Color.R + stop2.Color.R) / 2.0) * segmentWeight;
                g += ((stop1.Color.G + stop2.Color.G) / 2.0) * segmentWeight;
                b += ((stop1.Color.B + stop2.Color.B) / 2.0) * segmentWeight;
                a += ((stop1.Color.A + stop2.Color.A) / 2.0) * segmentWeight;

                totalWeight += segmentWeight;
            }

            // Normalize by total weight
            double weightFactor = totalWeight > 0 ? 1.0 / totalWeight : 1.0;
            return new Color(
                (byte)(a * weightFactor),
                (byte)(r * weightFactor),
                (byte)(g * weightFactor),
                (byte)(b * weightFactor)
            );
        }
    }
}