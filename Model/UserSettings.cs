using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace JagexAccountSwitcher.Model;

public class UserSettings : INotifyPropertyChanged
{
    private string _selectedLanguage = "English";
    private string _runelitePath = string.Empty;
    private string _configurationsPath = string.Empty;
    private string _microBotJarPath = string.Empty;
    private string _discordWebhookUrl = string.Empty;

    public string SelectedLanguage { get => _selectedLanguage; set { _selectedLanguage = value; OnPropertyChanged(); } }
    public string RunelitePath { get => _runelitePath; set { _runelitePath = value; OnPropertyChanged(); } }
    public string ConfigurationsPath { get => _configurationsPath; set { _configurationsPath = value; OnPropertyChanged(); } }
    public string MicroBotJarPath { get => _microBotJarPath; set { _microBotJarPath = value; OnPropertyChanged(); } }
    public string DiscordWebhookUrl { get => _discordWebhookUrl; set { _discordWebhookUrl = value; OnPropertyChanged(); } }

    public ObservableCollection<RunescapeAccount> Accounts { get; set; } = new();

    private static string ConfigPath => Path.Combine(Directory.GetCurrentDirectory(), "Configurations", "settings.json");

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void SaveToFile()
    {
        try {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        } catch { }
    }

    public void LoadFromFile()
    {
        if (!File.Exists(ConfigPath)) return;
        try {
            var loaded = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(ConfigPath));
            if (loaded != null) {
                SelectedLanguage = loaded.SelectedLanguage;
                RunelitePath = loaded.RunelitePath;
                ConfigurationsPath = loaded.ConfigurationsPath;
                MicroBotJarPath = loaded.MicroBotJarPath;
                DiscordWebhookUrl = loaded.DiscordWebhookUrl;
                Accounts.Clear();
                if (loaded.Accounts != null) foreach (var acc in loaded.Accounts) Accounts.Add(acc);
            }
        } catch { }
    }
}
