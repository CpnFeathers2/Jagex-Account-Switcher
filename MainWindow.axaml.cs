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
        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configurations");

        if (!Directory.Exists(configPath))
        {
            return;
        }

        var accounts = new List<RunescapeAccount>();
        
        foreach (var dir in Directory.GetDirectories(configPath))
        {
            string credPath = Path.Combine(dir, "credentials.properties");
            if (!File.Exists(credPath)) continue;

            var lines = File.ReadAllLines(credPath);
            var usernameLine = lines.FirstOrDefault(l => l.StartsWith("username="));
            if (usernameLine == null) continue;

            string rsn = usernameLine.Split('=')[1].Trim();
            string relativePath = Path.Combine(Path.GetFileName(dir), "credentials.properties").Replace("\\", "/");

       accounts.Add(new RunescapeAccount
            {
            AccountName = rsn,
            FilePath = relativePath
           });
        }

        string jsonPath = Path.Combine(configPath, "accounts.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(accounts, new JsonSerializerOptions { WriteIndented = true }));
    }
}
