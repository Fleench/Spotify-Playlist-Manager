#ifndef THEME_LOGIC_HPP
#define THEME_LOGIC_HPP

#include <string>
#include <tuple>
#include <algorithm>
#include <cmath>
#include <sstream>
#include <iomanip>

namespace SpotifyPlaylistManager {

enum class ThemeMode { Light, Dark };

struct ThemeColors {
    std::string Surface;
    std::string Accent;
    std::string SurfaceTint;
    std::string SurfaceHighlight;
    std::string TextOnSurface;
    std::string TextOnSurfaceTint;
};

class ThemeLogic {
public:
    ThemeMode ActiveMode;
    ThemeColors DarkColors;
    ThemeColors LightColors;

    ThemeLogic(const std::string& surfaceHex = "#194D4D", ThemeMode mode = ThemeMode::Dark) {
        Generate(surfaceHex);
        ActiveMode = mode;
    }

    ThemeColors GetColors() const {
        return ActiveMode == ThemeMode::Dark ? DarkColors : LightColors;
    }

    void Generate(const std::string& surfaceHex) {
        auto baseColor = FromHex(surfaceHex);
        auto [h, s, l] = ToHsl(baseColor);

        DarkColors = {
            ToHex(baseColor),
            ToHex(FromHsl(WrapHue(h - 38), Clamp01(s * 0.47), Clamp01(l + 0.398))),
            ToHex(FromHsl(WrapHue(h + 9), Clamp01(s * 0.431), Clamp01(l + 0.031))),
            ToHex(FromHsl(WrapHue(h + 9), Clamp01(s * 0.418), Clamp01(l + 0.039))),
            ToHex(FromHsl(WrapHue(h - 26.4), Clamp01(s * 0.40), Clamp01(l + 0.278))),
            ToHex(FromHsl(WrapHue(h - 20.6), Clamp01(s * 0.275), Clamp01(l + 0.247)))
        };

        auto lightBase = FromHsl(h, Clamp01(s * 0.45), Clamp01(1 - l * 0.65));
        auto [lh, ls, ll] = ToHsl(lightBase);

        LightColors = {
            ToHex(lightBase),
            ToHex(FromHsl(WrapHue(lh - 38), Clamp01(ls * 0.65), Clamp01(ll + 0.25))),
            ToHex(FromHsl(WrapHue(lh + 10), Clamp01(ls * 0.35), Clamp01(ll - 0.06))),
            ToHex(FromHsl(WrapHue(lh + 10), Clamp01(ls * 0.40), Clamp01(ll - 0.04))),
            ToHex(FromHsl(WrapHue(lh + 180), Clamp01(ls * 0.80), Clamp01(1 - ll * 0.6))),
            ToHex(FromHsl(WrapHue(lh + 180), Clamp01(ls * 0.70), Clamp01(1 - ll * 0.55)))
        };
    }

    void Swap() {
        ActiveMode = ActiveMode == ThemeMode::Dark ? ThemeMode::Light : ThemeMode::Dark;
    }

private:
    struct Color { uint8_t r, g, b; };

    static Color FromHex(const std::string& hex) {
        std::string h = hex[0] == '#' ? hex.substr(1) : hex;
        if (h.length() != 6) return {0, 0, 0};
        unsigned int val;
        std::stringstream ss;
        ss << std::hex << h;
        ss >> val;
        return { static_cast<uint8_t>((val >> 16) & 0xFF), static_cast<uint8_t>((val >> 8) & 0xFF), static_cast<uint8_t>(val & 0xFF) };
    }

    static std::string ToHex(Color c) {
        std::stringstream ss;
        ss << "#" << std::setfill('0') << std::setw(2) << std::hex << (int)c.r
           << std::setfill('0') << std::setw(2) << std::hex << (int)c.g
           << std::setfill('0') << std::setw(2) << std::hex << (int)c.b;
        std::string result = ss.str();
        std::transform(result.begin(), result.end(), result.begin(), ::toupper);
        return result;
    }

    static std::tuple<double, double, double> ToHsl(Color c) {
        double r = c.r / 255.0, g = c.g / 255.0, b = c.b / 255.0;
        double max = std::max({r, g, b}), min = std::min({r, g, b});
        double h = 0, s = 0, l = (max + min) / 2.0;

        if (max != min) {
            double d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
            if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / d + 2;
            else if (max == b) h = (r - g) / d + 4;
            h /= 6.0;
        }
        return { h * 360.0, s, l };
    }

    static double HueToRgb(double p, double q, double t) {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1/6.0) return p + (q - p) * 6 * t;
        if (t < 1/2.0) return q;
        if (t < 2/3.0) return p + (q - p) * (2/3.0 - t) * 6;
        return p;
    }

    static Color FromHsl(double h, double s, double l) {
        h /= 360.0;
        double r, g, b;
        if (s == 0) {
            r = g = b = l;
        } else {
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p = 2 * l - q;
            r = HueToRgb(p, q, h + 1/3.0);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1/3.0);
        }
        return { static_cast<uint8_t>(std::round(r * 255)), static_cast<uint8_t>(std::round(g * 255)), static_cast<uint8_t>(std::round(b * 255)) };
    }

    static double WrapHue(double h) {
        while (h < 0) h += 360;
        while (h >= 360) h -= 360;
        return h;
    }

    static double Clamp01(double v) {
        return std::max(0.0, std::min(1.0, v));
    }
};

}

#endif // THEME_LOGIC_HPP
