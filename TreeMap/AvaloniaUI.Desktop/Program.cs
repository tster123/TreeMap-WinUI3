﻿using System;

using Avalonia;
using Avalonia.Rendering.Composition;
using AvaloniaUI.Views;
using Serilog;

namespace AvaloniaUI.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("C:\\temp\\logs\\TreeMap.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new CompositionOptions()
            {
                UseRegionDirtyRectClipping = true
            })
            .WithInterFont()
            .LogToTrace();

}
