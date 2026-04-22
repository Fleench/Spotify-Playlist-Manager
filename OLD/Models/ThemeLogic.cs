/* File: ThemeLogic.cs
 * Author: Glenn Sutherland, ChatGPT Codex
 * Description: Generates light and dark theme palettes from a base surface color.
 */

using System;
using System.Globalization;
using Avalonia.Media;


namespace Spotify_Playlist_Manager.Models
{
    public enum ThemeMode { Light, Dark }

    /// <summary>
    /// Encapsulates theme generation and swapping logic. Given a seed color the
    /// class can produce cohesive light and dark palettes for the Avalonia UI.
    /// </summary>
    public class Theme
    {
        public ThemeMode ActiveMode { get; private set; }

        /// <summary>
        /// Gets the color palette for the currently active <see cref="ThemeMode"/>.
        /// </summary>
        public ThemeColors Colors => ActiveMode == ThemeMode.Dark ? DarkColors : LightColors;
        public ThemeColors DarkColors { get; private set; }
        public ThemeColors LightColors { get; private set; }

        public Theme(string surfaceHex = "#194D4D", ThemeMode mode = ThemeMode.Dark)
        {
            Generate(surfaceHex);
            ActiveMode = mode;
        }

        /// <summary>
        /// Generate both light and dark variants based on a single surface.
        /// </summary>
        public void Generate(string surfaceHex)
        {
            var baseColor = FromHex(surfaceHex);
            var (h, s, l) = ToHsl(baseColor);

            // --- DARK THEME ---
            var dark = new ThemeColors
            {
                Surface = ToHex(baseColor),
                Accent = ToHex(FromHsl(WrapHue(h - 38), Clamp01(s * 0.47), Clamp01(l + 0.398))),
                SurfaceTint = ToHex(FromHsl(WrapHue(h + 9), Clamp01(s * 0.431), Clamp01(l + 0.031))),
                SurfaceHighlight = ToHex(FromHsl(WrapHue(h + 9), Clamp01(s * 0.418), Clamp01(l + 0.039))),
                TextOnSurface = ToHex(FromHsl(WrapHue(h - 26.4), Clamp01(s * 0.40), Clamp01(l + 0.278))),
                TextOnSurfaceTint = ToHex(FromHsl(WrapHue(h - 20.6), Clamp01(s * 0.275), Clamp01(l + 0.247)))
            };

            // --- LIGHT THEME ---
            // Flip math ranges (lighter surface, darker text)
            var lightBase = FromHsl(h, Clamp01(s * 0.45), Clamp01(1 - l * 0.65));
            var (lh, ls, ll) = ToHsl(lightBase);
            var light = new ThemeColors
            {
                Surface = ToHex(lightBase),
                Accent = ToHex(FromHsl(WrapHue(lh - 38), Clamp01(ls * 0.65), Clamp01(ll + 0.25))),
                SurfaceTint = ToHex(FromHsl(WrapHue(lh + 10), Clamp01(ls * 0.35), Clamp01(ll - 0.06))),
                SurfaceHighlight = ToHex(FromHsl(WrapHue(lh + 10), Clamp01(ls * 0.40), Clamp01(ll - 0.04))),
                TextOnSurface = ToHex(FromHsl(WrapHue(lh + 180), Clamp01(ls * 0.80), Clamp01(1 - ll * 0.6))),
                TextOnSurfaceTint = ToHex(FromHsl(WrapHue(lh + 180), Clamp01(ls * 0.70), Clamp01(1 - ll * 0.55)))
            };

            DarkColors = dark;
            LightColors = light;
        }

        /// <summary>
        /// Swap between light and dark themes.
        /// </summary>
        public void Swap()
        {
            ActiveMode = ActiveMode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
        }

        // ============ Output ============
        public override string ToString()
        {
            string header = $"\n{ActiveMode} Theme\n";
            string swatch(ThemeColors c, string n, string hex)
            {
                var col = FromHex(hex);
                return $"{n,-20}: {AnsiBg(col)}  {AnsiReset} {hex}";
            }

            var current = Colors;
            return header +
                   swatch(current, "Surface", current.Surface) + "\n" +
                   swatch(current, "Accent", current.Accent) + "\n" +
                   swatch(current, "SurfaceTint", current.SurfaceTint) + "\n" +
                   swatch(current, "SurfaceHighlight", current.SurfaceHighlight) + "\n" +
                   swatch(current, "TextOnSurface", current.TextOnSurface) + "\n" +
                   swatch(current, "TextOnSurfaceTint", current.TextOnSurfaceTint) + "\n";
        }

        // ============ Helpers ============
        private static (double h, double s, double l) ToHsl(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double h, s, l = (max + min) / 2.0;
            if (max == min) { h = s = 0; }
            else
            {
                double d = max - min;
                s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
                if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
                else if (max == g) h = (b - r) / d + 2;
                else h = (r - g) / d + 4;
                h *= 60;
            }
            return (h, s, l);
        }

        private static Color FromHsl(double h, double s, double l)
        {
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = l - c / 2;
            double r, g, b;

            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return Color.FromRgb(
                (byte)Math.Round((r + m) * 255),
                (byte)Math.Round((g + m) * 255),
                (byte)Math.Round((b + m) * 255)
            );
        }

        private static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        private static Color FromHex(string hex)
        {
            hex = hex.Replace("#", "");
            return Color.FromRgb(
                byte.Parse(hex[..2], NumberStyles.HexNumber),
                byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber),
                byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber)
            );
        }
        private static double Clamp01(double v) => Math.Max(0, Math.Min(1, v));
        private static double WrapHue(double h) => h < 0 ? h + 360 : h % 360;

        private static string AnsiBg(Color c) => $"\u001b[48;2;{c.R};{c.G};{c.B}m  ";
        private const string AnsiReset = "\u001b[0m";
    }

    /// <summary>
    /// Represents a complete palette generated for a specific theme mode.
    /// </summary>
    public struct ThemeColors
    {
        public string Surface { get; set; }
        public string Accent { get; set; }
        public string SurfaceTint { get; set; }
        public string SurfaceHighlight { get; set; }
        public string TextOnSurface { get; set; }
        public string TextOnSurfaceTint { get; set; }
    }
}
