using Avalonia;
using System;
using System.IO;
using System.Runtime.InteropServices;
using JagexAccountSwitcher.Helpers;

namespace JagexAccountSwitcher;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // On Linux/macOS, automatically enable console output
        // This ensures you can see debug output when launching the app
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ConsoleHelper.ShowConsole();
            Console.WriteLine("=== JagexAccountSwitcher Started ===");
            Console.WriteLine($"Platform: {RuntimeInformation.OSDescription}");
            Console.WriteLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine("=====================================");
        }
        
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // Log to file
            using StreamWriter sw = File.AppendText("error.log");
            sw.WriteLine(ex.ToString());
            
            // Also log to console if visible
            Console.WriteLine($"FATAL ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .With(new X11PlatformOptions
        {
            // Forces software rendering to avoid the LLVMpipe/OpenGL crash
            RenderingMode = new[] { X11RenderingMode.Software }
        })
        .LogToTrace();
}
