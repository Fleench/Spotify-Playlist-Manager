# What is Avalonia?

Avalonia is a free and open-source .NET cross-platform UI framework. It is inspired by Windows Presentation Foundation (WPF) and allows developers to create user interfaces for desktop, mobile, and web applications from a single codebase.

## Key Features

*   **Cross-Platform:** Avalonia supports a wide range of platforms, including:
    *   Windows
    *   macOS
    *   Linux
    *   iOS
    *   Android
    *   WebAssembly
*   **XAML-Based:** Avalonia uses the Extensible Application Markup Language (XAML) for defining user interfaces, which is familiar to developers with experience in WPF, UWP, or Xamarin.Forms.
*   **Independent Rendering Engine:** Unlike many other UI frameworks that rely on native platform controls, Avalonia uses its own rendering engine (powered by Skia or Direct2D). This ensures that the application's UI looks and behaves identically across all supported platforms, providing a consistent user experience.
*   **Flexible Styling System:** Avalonia has a powerful and flexible styling system, similar to CSS, that allows for extensive customization of the application's appearance.
*   **.NET Integration:** As a .NET framework, Avalonia allows developers to write application logic in C# or any other .NET language, leveraging the full power and ecosystem of the .NET platform.

## How it Works

Avalonia's architecture consists of a platform-agnostic core layer and a platform-specific integration layer. The core layer handles UI controls, layout, styling, data binding, and input. The rendering engine is part of this core, drawing the UI pixel by pixel, which is how it achieves its cross-platform consistency. The platform integration layer handles the interaction with the underlying operating system for things like windowing, input, and other platform-specific services.

# Avalonia Project Structure

This document explains the purpose of each `.cs` file in the project.

## Root Directory

### `Program.cs`

This is the main entry point for the application. It initializes the Avalonia framework and starts the application.

### `App.axaml.cs`

This file is the code-behind for `App.axaml`. It handles application-level events and sets up the main window.

### `ViewLocator.cs`

This class is responsible for locating and creating views for a given view model. It allows for a convention-based approach to connecting views and view models.

## ViewModels Directory

### `ViewModelBase.cs`

This is a base class for all view models in the application. It typically implements `INotifyPropertyChanged` to support data binding.

### `MainWindowViewModel.cs`

This is the view model for the main window. It contains the logic and data that the main window will display.

## Views Directory

### `MainWindow.axaml.cs`

This is the code-behind for the main window (`MainWindow.axaml`). It's responsible for handling UI events and interacting with the view model.
