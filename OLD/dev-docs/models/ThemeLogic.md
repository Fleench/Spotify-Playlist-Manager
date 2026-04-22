# ThemeLogic.cs

## Overview
`ThemeLogic` generates cohesive light and dark color palettes for the Avalonia UI based on a single seed surface color. It exposes a `Theme` class that tracks the active palette and utilities for converting between RGB and HSL color spaces.

## Key Responsibilities
- **Palette generation:** Converts the seed color into complementary tints, accents, and text colors for both light and dark modes.
- **Mode switching:** Exposes `Swap()` to toggle between light and dark palettes at runtime.
- **Color conversions:** Provides helper methods for HSL <-> RGB conversion and ANSI swatch formatting (useful for console previews).

## Important Members
- `Theme.Generate(string surfaceHex)` – Recomputes both palettes when the user selects a new base color.
- `Theme.Colors` – Returns the currently active `ThemeColors` struct.
- `ThemeColors` struct – Stores the six core color values (surface, accent, tints, and text colors).

## Usage Notes
Construct a `Theme` with the desired default surface color (hex string). Call `Generate` whenever the user picks a new base tone, and then bind UI resources to properties inside `ThemeColors`.
