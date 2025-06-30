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

  private UserSettings _userSettings;

    public MainWindow()
    {
        InitializeComponent();
_userSettings = new UserSettings();
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

    // 🔁 Reformat credentials.0001.properties → credentials.properties.0001
    var configFiles = Directory.GetFiles(configPath, "credentials.*.properties");
    foreach (var file in configFiles)
    {
        var fileName = Path.GetFileName(file);
        var parts = fileName.Split('.');
        if (parts.Length == 3 && int.TryParse(parts[1], out var number))
        {
            var correctedName = $"credentials.properties.{parts[1]}";
            var correctedPath = Path.Combine(configPath, correctedName);
            if (!File.Exists(correctedPath))
            {
                File.Move(file, correctedPath);
                Console.WriteLine($"🔁 Renamed: {fileName} → {correctedName}");
            }
        }
    }

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
    var merged = new List<RunescapeAccount>();

    foreach (var credPath in Directory.GetFiles(configPath, "credentials.properties.*"))
    {
        var name = CredentialsHelper.GetDisplayNameOrFallback(credPath);

        var existing = accounts?.FirstOrDefault(a =>
            string.Equals(a.FilePath, credPath, StringComparison.OrdinalIgnoreCase));

        var account = existing ?? new RunescapeAccount();
        account.AccountName = name;
        account.FilePath = credPath;

        merged.Add(account);
    }

    // Set active account based on the currently active credentials.properties
    var activeCredPath = Path.Combine(_userSettings.RunelitePath, "credentials.properties");
    if (File.Exists(activeCredPath))
    {
        var activeName = CredentialsHelper.GetDisplayNameOrFallback(activeCredPath);
        foreach (var acc in merged)
            acc.IsActiveAccount = acc.AccountName.Equals(activeName, StringComparison.OrdinalIgnoreCase);
    }

    // Save updated list back to file
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(merged, new JsonSerializerOptions { WriteIndented = true }));

    // Update UI binding
    overview.Accounts.Clear();
    foreach (var acc in merged)
        overview.Accounts.Add(acc);

    Console.WriteLine($"UI refreshed with {merged.Count} account(s).");
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