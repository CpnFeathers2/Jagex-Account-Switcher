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
        this.Width = 1100;  // Adjust this number as needed
    	this.Height = 700;
        
        // REMOVED: _userSettings = new UserSettings(); 
        // We do not want a new instance here; we want to use the one passed to DataContext.

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
        if (Directory.Exists(configPath))
        {
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
        }
        else
        {
            Directory.CreateDirectory(configPath);
        }
        
        // Load existing accounts from JSON (or create empty list)
        List<RunescapeAccount> accounts = new List<RunescapeAccount>();
        if (File.Exists(jsonPath))
        {
            try
            {
                var json = File.ReadAllText(jsonPath);
                var loadedAccounts = JsonSerializer.Deserialize<List<RunescapeAccount>>(json);
                if (loadedAccounts != null)
                {
                    accounts = loadedAccounts;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading JSON: {ex.Message}");
            }
        }
        
        // Build merged account list from credential files
        var merged = new List<RunescapeAccount>();
        if (Directory.Exists(configPath))
        {
            foreach (var credPath in Directory.GetFiles(configPath, "credentials.properties.*"))
            {
                var fileName = Path.GetFileName(credPath);
                var accountId = fileName.Substring("credentials.properties.".Length); // e.g., "0001"
                var name = accountId;
                var existing = accounts?.FirstOrDefault(a =>
                    string.Equals(a.FilePath, credPath, StringComparison.OrdinalIgnoreCase));
                var account = existing ?? new RunescapeAccount();
                account.AccountName = name;
                account.FilePath = credPath;
                merged.Add(account);
            }
        }
        
        // Check if we found any credential files
        if (merged.Count == 0)
        {
            Console.WriteLine("No credentials.properties.* files found in Configurations folder.");
            return;
        }
        Console.WriteLine($"Found {merged.Count} credential files.");
        
        // FIX: Get RunelitePath correctly from ViewModel (Source of Truth) or Fallback
        string runelitePath = RuneliteHelper.GetRunelitePath(); // Default fallback
        if (DataContext is MainWindowViewModel vm && vm.SettingsViewModel != null)
        {
            if (!string.IsNullOrEmpty(vm.SettingsViewModel.RunelitePath))
            {
                runelitePath = vm.SettingsViewModel.RunelitePath;
            }
        }

        // Set active account based on the currently active credentials.properties
        var activeCredPath = Path.Combine(runelitePath, "credentials.properties");
        
        if (File.Exists(activeCredPath))
        {
            var activeName = CredentialsHelper.GetDisplayNameOrFallback(activeCredPath);
            foreach (var acc in merged)
                acc.IsActiveAccount = acc.AccountName.Equals(activeName, StringComparison.OrdinalIgnoreCase);
        }
        
        // Save updated list back to file
        try
        {
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(merged, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("Updated accounts.json with merged data.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving JSON: {ex.Message}");
        }
        
        // Update UI binding
        if (DataContext is MainWindowViewModel mainViewModel &&
            mainViewModel.AccountOverviewViewModel is AccountOverviewViewModel overview)
        {
            overview.Accounts.Clear();
            foreach (var acc in merged)
                overview.Accounts.Add(acc);
            Console.WriteLine($"UI refreshed with {merged.Count} account(s).");
        }
        else
        {
            Console.WriteLine("⚠ Could not update UI — AccountOverviewViewModel not found.");
        }
    }
}
