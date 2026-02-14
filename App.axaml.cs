using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using JagexAccountSwitcher.Converters;
using JagexAccountSwitcher.Model;
using JagexAccountSwitcher.ViewModels;
using Jeek.Avalonia.Localization;
using JagexAccountSwitcher.Services;

namespace JagexAccountSwitcher;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        StartUp();
    }

    private void StartUp()
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "Configurations");
        if (!Path.Exists(configPath))
        {
            Directory.CreateDirectory(configPath);
        }
    }

    public override void OnFrameworkInitializationCompleted()
{
    // 1. Setup Configuration
    var config = new UserSettings();
    config.LoadFromFile();

    // 2. Initialize Services
    var launcherService = new LauncherService(config);
    var healthMonitor = new ProcessMonitorService(launcherService);

    // 3. Setup Localization
    Localizer.SetLocalizer(new ResXLocalizer());
    Localizer.Language = config.SelectedLanguage switch
    {
        "Spanish" => "es",
        "Portuguese" => "pt",
        _ => "en"
    };

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.MainWindow = new MainWindow();
        
        // 4. Pass services into the ViewModel
        desktop.MainWindow.DataContext = new MainWindowViewModel(
            desktop.MainWindow, 
            config, 
            launcherService, 
            healthMonitor);
    }

    base.OnFrameworkInitializationCompleted();
}
}
