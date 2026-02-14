using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using JagexAccountSwitcher.Model;
using JagexAccountSwitcher.Services;
// Force usage of your specific Helper
using RelayCommand = JagexAccountSwitcher.Helpers.RelayCommand; 

namespace JagexAccountSwitcher.ViewModels;

public class MassAccountHandlerViewModel : INotifyPropertyChanged
{
    private readonly AccountOverviewViewModel _viewModel;
    private readonly UserSettings _settings;
    private readonly LauncherService _launcherService;
    private readonly ProcessMonitorService _monitorService;
    
    private ObservableCollection<MassAccountLinkerModel> _accountProcesses;
    private int _startAllLaunchDelay = 5000;
    
    // --- FIX: Add cancellation token source for StartAll loop ---
    private CancellationTokenSource _startAllCts;

    public ObservableCollection<MassAccountLinkerModel> AccountProcesses
    {
        get => _accountProcesses;
        set { _accountProcesses = value; OnPropertyChanged(); }
    }

    public int StartAllLaunchDelay
    {
        get => _startAllLaunchDelay;
        set { _startAllLaunchDelay = value; OnPropertyChanged(); }
    }

    public ICommand StartAllCommand { get; }
    public ICommand KillClientCommand { get; }
    public ICommand StartClientCommand { get; }
    public ICommand KillAllCommand { get; }

    public MassAccountHandlerViewModel(AccountOverviewViewModel overviewVm, UserSettings settings, LauncherService launcher, ProcessMonitorService monitor)
    {
        _viewModel = overviewVm;
        _settings = settings;
        _launcherService = launcher;
        _monitorService = monitor;
        _accountProcesses = new ObservableCollection<MassAccountLinkerModel>();

        StartAllCommand = new RelayCommand(StartAllAccounts);
        KillClientCommand = new JagexAccountSwitcher.Helpers.RelayCommand<MassAccountLinkerModel>(KillClient);
        StartClientCommand = new JagexAccountSwitcher.Helpers.RelayCommand<MassAccountLinkerModel>(StartClient);
        KillAllCommand = new RelayCommand(KillAllClients);
        
        _launcherService.OnBreakStatusChanged = (accountId, isOnBreak, endTime) =>
    {
        // Use the UI Thread to update the model
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var model = AccountProcesses.FirstOrDefault(a => a.Account.AccountName == accountId);
            if (model != null)
            {
                model.IsOnBreak = isOnBreak;
                model.BreakEndTime = endTime;
                
                // If the break is starting, we ensure the process is null 
                // (though the Java client usually handles its own exit)
                if (isOnBreak) model.Process = null;
            }
        });
    };

        // Subscribe to changes in the AccountOverview's Accounts collection
        if (_viewModel.Accounts != null)
        {
            _viewModel.Accounts.CollectionChanged += (s, e) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    PopulateAccountProcesses();
                });
            };
        }

        PopulateAccountProcesses();
    }

    // ACTIVE ACCOUNTS Display Property
    public string ActiveAccountsDisplay => $"ACTIVE ACCOUNTS: {AccountProcesses.Count(a => a.IsRunning)}";

    // NEW: TOTAL ACCOUNTS Display Property
    public string TotalAccountsDisplay => $"TOTAL ACCOUNTS: {GetTotalAccountsCount()}";

    // Helper method to refresh both counts
    public void RefreshActiveCount() => OnPropertyChanged(nameof(ActiveAccountsDisplay));
    
    // NEW: Helper method to refresh total accounts count
    public void RefreshTotalCount() => OnPropertyChanged(nameof(TotalAccountsDisplay));

    // NEW: Method to count total credential files in the Configurations folder
    private int GetTotalAccountsCount()
    {
        try
        {
            // Get the configurations path from settings
            string configDir = _settings.ConfigurationsPath;
            
            // Guard against empty paths
            if (string.IsNullOrWhiteSpace(configDir) || !Directory.Exists(configDir))
            {
                return 0;
            }

            // Count all credential property files
            // Pattern matches: credentials.properties.*, credentials.*.properties
            var credentialFiles = Directory.GetFiles(configDir, "credentials*.properties*")
                .Where(f => 
                {
                    var fileName = Path.GetFileName(f);
                    // Match credentials.properties.XXXX or credentials.XXXX.properties
                    return fileName.StartsWith("credentials") && 
                           (fileName.Contains(".properties.") || fileName.EndsWith(".properties"));
                })
                .ToList();

            return credentialFiles.Count();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MassAccountHandler] Error counting credential files: {ex.Message}");
            return 0;
        }
    }

    // Update the Populate method to properly sync with AccountOverview
    private void PopulateAccountProcesses()
    {
        if (_viewModel?.Accounts == null) return;
        
        // Remove models for accounts that no longer exist
        var accountsToRemove = _accountProcesses
            .Where(p => !_viewModel.Accounts.Any(a => a.AccountName == p.Account.AccountName))
            .ToList();
        
        foreach (var model in accountsToRemove)
        {
            _accountProcesses.Remove(model);
        }
        
        // Add or update accounts
        foreach (var acc in _viewModel.Accounts)
        {
            var existingModel = _accountProcesses.FirstOrDefault(p => p.Account.AccountName == acc.AccountName);
            
            if (existingModel == null)
            {
                // Add new account
                var model = new MassAccountLinkerModel { Account = acc };
                
                // Listen for when IsRunning changes to update our footer counter
                model.PropertyChanged += (s, e) => {
                    if (e.PropertyName == nameof(MassAccountLinkerModel.IsRunning))
                    {
                        RefreshActiveCount();
                    }
                };
                
                _accountProcesses.Add(model);
            }
            else
            {
                // Update existing account reference to ensure data is synced
                // Keep the process and state, but update the account data
                existingModel.Account = acc;
            }
        }
        
        RefreshActiveCount();
        RefreshTotalCount();
    }
    
    // Add a public method that can be called when accounts are refreshed
    public void RefreshAccountsList()
    {
        PopulateAccountProcesses();
    }
    

    private void StartClient(MassAccountLinkerModel model)
    {
        Task.Run(() => 
        {
            var process = _launcherService.StartClient(model.Account.AccountName, model.Account.ClientArguments ?? "");
            Dispatcher.UIThread.Post(() =>
            {
                model.Process = process;
            });
        });
    }

    private void KillClient(MassAccountLinkerModel model)
    {
        if (model.Process != null && !model.Process.HasExited)
        {
            try
            {
                model.Process.Kill();
                model.Process = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to kill process: {ex.Message}");
            }
        }
    }

    // --- FIX: Use cancellation token to break the loop properly ---
    private async void StartAllAccounts()
    {
        // If already running, stop the existing loop
        if (_startAllCts != null && !_startAllCts.Token.IsCancellationRequested)
        {
            _startAllCts.Cancel();
            _startAllCts = null;
            return;
        }

        // Create a new cancellation token for this launch cycle
        _startAllCts = new CancellationTokenSource();
        var token = _startAllCts.Token;

        try
        {
            foreach (var model in AccountProcesses)
            {
                // Check if cancelled
                if (token.IsCancellationRequested)
                    break;

                // Only start if not already running
                if (model.Process == null || model.Process.HasExited)
                {
                    StartClient(model);
                    await Task.Delay(StartAllLaunchDelay, token); // Pass token to delay
                }
            }
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("[MassAccountHandler] Start all was cancelled.");
        }
        finally
        {
            _startAllCts = null;
        }
    }

    private void KillAllClients()
    {
        foreach (var model in AccountProcesses)
        {
            KillClient(model);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
