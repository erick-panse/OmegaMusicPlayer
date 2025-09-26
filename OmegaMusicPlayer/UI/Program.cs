using Avalonia;
using Avalonia.Logging;
using System;

namespace OmegaMusicPlayer.UI
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
            .With(new Win32PlatformOptions { OverlayPopups = true }) // prevents pop up from overlaying others apps when Omega is not focused
            .With(new AvaloniaNativePlatformOptions() { OverlayPopups = true }) // same as above but for MacOS
            .With(new X11PlatformOptions { OverlayPopups = true }) // same as above but for Unix/Linux
            .WithInterFont()
            .LogToTrace(LogEventLevel.Verbose);
    }
}
