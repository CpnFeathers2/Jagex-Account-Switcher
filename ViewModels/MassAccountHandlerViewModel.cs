using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using JagexAccountSwitcher.Helpers;
using JagexAccountSwitcher.Model;
using RelayCommand = JagexAccountSwitcher.Helpers.RelayCommand;

namespace JagexAccountSwitcher.ViewModels
{
    public class MassAccountHandlerViewModel : INotifyPropertyChanged
    {
        private readonly AccountOverviewViewModel _viewModel;
        private readonly UserSettings _settings;
        private readonly ObservableCollection<RunescapeAccount> _accounts;
        
        private ObservableCollection<MassAccountLinkerModel> _accountProcesses;
        private int _updateDelay = 1000;
        private bool _showClientOutput;

        public ObservableCollection<MassAccountLinkerModel> AccountProcesses
        {
            get => _accountProcesses;
            set
            {
                _accountProcesses = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<RunescapeAccount> Accounts => _accounts;

        public int UpdateDelay
        {
            get => _updateDelay;
            set
            {
                if (_updateDelay != value)
                {
                    _updateDelay = value;
                    OnPropertyChanged();
                }
            }
        }

#if WINDOWS
        public bool ShowClientOutput
        {
            get => _showClientOutput;
            set
            {
                if (_showClientOutput != value)
                {
                    _showClientOutput = value;
                    OnPropertyChanged();
                    
                    // Platform-specific console toggle
                    if (value)
                        ConsoleHelper.ShowConsole();
                    else
                        ConsoleHelper.HideConsole();
                }
            }
        }
#endif

        public ICommand StartAllCommand { get; }
        public ICommand KillClientCommand { get; }
        public ICommand StartClientCommand { get; }

        // Parameterless constructor for design-time support
        public MassAccountHandlerViewModel()
        {
            // Initialize with empty/default values for design-time
            _viewModel = null;
            _settings = null;
            _accounts = new ObservableCollection<RunescapeAccount>();
            _accountProcesses = new ObservableCollection<MassAccountLinkerModel>();
            
            // Initialize commands with empty actions for design-time
            StartAllCommand = new RelayCommand(() => { });
            KillClientCommand = new JagexAccountSwitcher.Helpers.RelayCommand<MassAccountLinkerModel>(_ => { });
            StartClientCommand = new JagexAccountSwitcher.Helpers.RelayCommand<MassAccountLinkerModel>(_ => { });
        }

        public MassAccountHandlerViewModel(AccountOverviewViewModel overviewVm, UserSettings settings, ObservableCollection<RunescapeAccount> accounts)
        {
            // Debug output
            System.Diagnostics.Debug.WriteLine($"MassAccountHandlerViewModel constructor called:");
            System.Diagnostics.Debug.WriteLine($"  overviewVm: {overviewVm != null}");
            System.Diagnostics.Debug.WriteLine($"  overviewVm.Accounts count: {overviewVm?.Accounts?.Count ?? 0}");
            System.Diagnostics.Debug.WriteLine($"  settings: {settings != null}");
            System.Diagnostics.Debug.WriteLine($"  accounts count: {accounts?.Count ?? 0}");

            // Initialize dependencies
            _viewModel = overviewVm ?? throw new ArgumentNullException(nameof(overviewVm));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));

            // Initialize collections
            _accountProcesses = new ObservableCollection<MassAccountLinkerModel>();

            // Initialize commands
            StartAllCommand = new RelayCommand(StartAllAccounts);
            KillClientCommand = new JagexAccountSwitcher.Helpers.RelayCommand<MassAccountLinkerModel>(KillClient);
            StartClientCommand = new JagexAccountSwitcher.Helpers.RelayCommand<MassAccountLinkerModel>(StartClient);

#if WINDOWS
            // Initialize ShowClientOutput based on current console state
            _showClientOutput = ConsoleHelper.IsConsoleVisible();
#endif

            // Populate AccountProcesses from the overview ViewModel's accounts
            PopulateAccountProcesses();

            // Subscribe to future changes in the accounts collection
            if (_viewModel?.Accounts != null)
            {
                _viewModel.Accounts.CollectionChanged += OnAccountsCollectionChanged;
            }

            // Start the background task for updating process lifetimes (only for runtime constructor)
            if (_viewModel != null)
            {
                Task.Run(UpdateProcessLifetimes);
            }
        }

        private void PopulateAccountProcesses()
        {
            _accountProcesses.Clear();

            if (_viewModel?.Accounts != null)
            {
                foreach (var account in _viewModel.Accounts)
                {
                    _accountProcesses.Add(new MassAccountLinkerModel { Account = account });
                }
            }
        }

        private void OnAccountsCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                PopulateAccountProcesses();
                OnPropertyChanged(nameof(AccountProcesses));
            });
        }

        private void KillClient(MassAccountLinkerModel model)
        {
            if (model != null)
            {
                ProcessHelper.KillClient(model);
            }
        }

        private void StartClient(MassAccountLinkerModel model)
        {
            if (model == null || _settings == null || string.IsNullOrWhiteSpace(_settings.MicroBotJarPath))
                return;

            // Check if already running
            if (model.Process != null && !model.Process.HasExited)
                return;

            if (RuneliteHelper.SetActiveAccount(model.Account, _viewModel.Accounts, _settings.ConfigurationsPath, _settings.RunelitePath))
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "javaw.exe",
                    Arguments = $"-jar{(model.Account.ClientArguments?.Contains("--developer-mode") == true ? " -ea " : string.Empty)} \"{_settings.MicroBotJarPath}\" {model.Account.ClientArguments}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var process = new Process { StartInfo = startInfo };

                // Set up output redirection
                process.OutputDataReceived += (sender, args) =>
                {
#if WINDOWS
                    if (!string.IsNullOrEmpty(args.Data) && ShowClientOutput)
#else
                    if (!string.IsNullOrEmpty(args.Data) && ConsoleHelper.IsConsoleVisible())
#endif
                        Console.WriteLine($"[{model.Account.AccountName}] {args.Data}");
                };

                process.ErrorDataReceived += (sender, args) =>
                {
#if WINDOWS
                    if (!string.IsNullOrEmpty(args.Data) && ShowClientOutput)
#else
                    if (!string.IsNullOrEmpty(args.Data) && ConsoleHelper.IsConsoleVisible())
#endif
                        Console.WriteLine($"[{model.Account.AccountName}] ERROR: {args.Data}");
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    model.Process = process;
                    model.ProcessLifetime = $"Runtime: {DateTime.Now - process.StartTime:hh\\:mm\\:ss}";
                });

                RuneliteHelper.SaveAccounts(_viewModel.Accounts, _settings.ConfigurationsPath);
            }
        }

        private async void StartAllAccounts()
        {
            if (_settings == null || string.IsNullOrWhiteSpace(_settings.MicroBotJarPath) || _viewModel?.Accounts == null)
                return;

            foreach (var account in _viewModel.Accounts)
            {
                // Check if the account is already running
                if (AccountProcesses.Any(x => x.Account == account && x.Process != null && !x.Process.HasExited))
                {
                    continue;
                }

                if (RuneliteHelper.SetActiveAccount(account, _viewModel.Accounts, _settings.ConfigurationsPath, _settings.RunelitePath))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "javaw.exe",
                        Arguments = $"-jar \"{_settings.MicroBotJarPath}\" {account.ClientArguments}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    var process = new Process { StartInfo = startInfo };

                    var model = AccountProcesses.FirstOrDefault(x => x.Account == account);
                    if (model != null)
                    {
                        var hasFullyLoaded = false;

                        process.OutputDataReceived += (sender, args) =>
                        {
                            if (args.Data?.Contains("Client initialization took") == true)
                            {
                                hasFullyLoaded = true;
                            }

#if WINDOWS
                            if (!string.IsNullOrEmpty(args.Data) && ShowClientOutput)
#else
                            if (!string.IsNullOrEmpty(args.Data) && ConsoleHelper.IsConsoleVisible())
#endif
                                Console.WriteLine($"[{account.AccountName}] {args.Data}");
                        };

                        process.ErrorDataReceived += (sender, args) =>
                        {
#if WINDOWS
                            if (!string.IsNullOrEmpty(args.Data) && ShowClientOutput)
#else
                            if (!string.IsNullOrEmpty(args.Data) && ConsoleHelper.IsConsoleVisible())
#endif
                                Console.WriteLine($"[{account.AccountName}] ERROR: {args.Data}");
                        };

                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            model.Process = process;
                            model.ProcessLifetime = $"Runtime: {DateTime.Now - process.StartTime:hh\\:mm\\:ss}";
                        });

                        // Wait for the client to fully load
                        var loadingTask = Task.Run(async () =>
                        {
                            while (!hasFullyLoaded && !process.HasExited)
                            {
                                Console.WriteLine("Waiting for account to fully load...");
                                await Task.Delay(1000);
                            }
                        });

                        await loadingTask;
                    }

                    RuneliteHelper.SaveAccounts(_viewModel.Accounts, _settings.ConfigurationsPath);
                }
            }
        }

        private async Task UpdateProcessLifetimes()
        {
            while (true)
            {
                foreach (var model in AccountProcesses.ToList()) // ToList to avoid collection modification issues
                {
                    if (model.Process != null && !model.Process.HasExited)
                    {
                        try
                        {
                            var runTime = DateTime.Now - model.Process.StartTime;
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                model.ProcessLifetime = $"Runtime: {runTime:hh\\:mm\\:ss}";
                            });
                        }
                        catch (Exception)
                        {
                            // Handle process access exceptions silently
                        }
                    }
                    else if (model.Process != null)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            model.Process = null;
                            model.ProcessLifetime = string.Empty;
                        });
                    }
                }

                await Task.Delay(UpdateDelay);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}