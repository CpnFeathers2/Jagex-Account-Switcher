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
    Console.WriteLine($"Looking for: {configPath}");

    if (!Directory.Exists(configPath))
    {
        Console.WriteLine("Directory doesn't exist!");
        return;
    }

    var credentialsFiles = Directory.GetFiles(configPath, "credentials.properties*")
        .Where(f => !f.EndsWith("accounts.json"))
        .ToArray();

    Console.WriteLine($"Found {credentialsFiles.Length} credentials files");

    var accounts = new List<RunescapeAccount>();

    foreach (var credFile in credentialsFiles)
    {
        Console.WriteLine($"\nProcessing file: {Path.GetFileName(credFile)}");

        try
        {
            var lines = File.ReadAllLines(credFile);
            Console.WriteLine($"File has {lines.Length} lines");

            var displayNameLine = lines.FirstOrDefault(l => l.StartsWith("JX_DISPLAY_NAME="));

            if (displayNameLine == null)
            {
                Console.WriteLine($"No JX_DISPLAY_NAME found in {Path.GetFileName(credFile)}, skipping");
                continue;
            }

            string displayName = displayNameLine.Split('=')[1].Trim();

            if (string.IsNullOrEmpty(displayName) || displayName == "Not set")
            {
                Console.WriteLine($"Display name is empty or 'Not set' in {Path.GetFileName(credFile)}, skipping");
                continue;
            }

            Console.WriteLine($"Found display name: {displayName}");

            string fileName = Path.GetFileName(credFile);

            accounts.Add(new RunescapeAccount
            {
                AccountName = displayName,
                FilePath = fileName
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing {Path.GetFileName(credFile)}: {ex.Message}");
        }
    }

    Console.WriteLine($"Total accounts found: {accounts.Count}");

    try
    {
        string jsonPath = Path.Combine(configPath, "accounts.json");
        string json = JsonSerializer.Serialize(accounts, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jsonPath, json);
        Console.WriteLine($"Accounts saved to: {jsonPath}");

        // UPDATE THE UI - Directly update AccountOverviewViewModel
        if (DataContext is MainWindowViewModel mainViewModel &&
            mainViewModel.AccountOverview is AccountOverviewViewModel overview)
        {
            Console.WriteLine("Updating UI via AccountOverviewViewModel...");

            overview.Accounts.Clear();
            foreach (var account in accounts)
            {
                overview.Accounts.Add(account);
            }

            Console.WriteLine($"UI updated with {accounts.Count} accounts");
        }
        else
        {
            Console.WriteLine("Could not update UI — AccountOverviewViewModel not found");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error saving accounts: {ex.Message}");
    }
}
}