using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using JagexAccountSwitcher.ViewModels;

namespace JagexAccountSwitcher.Views;

public partial class Settings : UserControl
{
    public Settings()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void BrowseRunelitePath_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            await viewModel.BrowseRunelitePath();
        }
        else
        {
            System.Console.WriteLine("ERROR: DataContext is not SettingsViewModel in BrowseRunelitePath_Click");
        }
    }
    
    private async void BrowseConfigurationsPath_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            await viewModel.BrowseConfigurationsPath();
        }
        else
        {
            System.Console.WriteLine("ERROR: DataContext is not SettingsViewModel in BrowseConfigurationsPath_Click");
        }
    }

    private async void BrowseMicrobotJarPath_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            await viewModel.BrowseMicrobotJarPath();
        }
        else
        {
            System.Console.WriteLine("ERROR: DataContext is not SettingsViewModel in BrowseMicrobotJarPath_Click");
        }
    }
    
    private async void DownloadLatestMicrobotJar_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            await viewModel.DownloadLatestMicrobotJar();
        }
        else
        {
            System.Console.WriteLine("ERROR: DataContext is not SettingsViewModel in DownloadLatestMicrobotJar_Click");
        }
    }
}
