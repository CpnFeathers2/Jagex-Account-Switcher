using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Interactivity;
using JagexAccountSwitcher.ViewModels;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System;
using JagexAccountSwitcher.Model;
using JagexAccountSwitcher.Helpers;
using System.Reflection;

namespace JagexAccountSwitcher;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.PreferSystemChrome;
        Background = Avalonia.Media.Brushes.Black;
        // Attach DevTools to this window
#if DEBUG
        this.AttachDevTools();
#endif
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

private async void RefreshConfigurations_Click(object? sender, RoutedEventArgs e)
{
    Console.WriteLine("Refresh button clicked!");

    string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configurations");
    string jsonPath = Path.Combine(configPath, "accounts.json");

    if (!File.Exists(jsonPath))
    {
        Console.WriteLine("No accounts.json file found!");
        return;
    }

    try
    {
        var json = File.ReadAllText(jsonPath);
        var accounts = JsonSerializer.Deserialize<List<RunescapeAccount>>(json);

        if (accounts == null || accounts.Count == 0)
        {
            Console.WriteLine("No accounts found in accounts.json.");
            return;
        }

        Console.WriteLine($"Loaded {accounts.Count} accounts from JSON.");

        // Save updated account list
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(accounts, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Updated accounts.json with new tokens.");

        // Update the UI (overview)
        if (DataContext is MainWindowViewModel mainViewModel &&
            mainViewModel.AccountOverview is AccountOverviewViewModel overview)
        {
            overview.Accounts.Clear();
            foreach (var account in accounts)
            {
                overview.Accounts.Add(account);
            }

            Console.WriteLine($"UI updated with {accounts.Count} accounts.");
        }
        else
        {
            Console.WriteLine("⚠ Could not update UI — AccountOverviewViewModel not found.");
        }

        // Optional: If your handler uses another ViewModel, update it here too
        // Example: mainViewModel.MassHandlerViewModel.Accounts = new ObservableCollection<RunescapeAccount>(accounts);

    }
    catch (Exception ex)
    {
        Console.WriteLine($"Unhandled error: {ex.Message}");
    }
}
}