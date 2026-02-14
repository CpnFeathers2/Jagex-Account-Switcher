using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Controls;
using JagexAccountSwitcher.Helpers;
using JagexAccountSwitcher.Model;
using JagexAccountSwitcher.Services;

namespace JagexAccountSwitcher.ViewModels
{
    public class MainWindowViewModel : ViewModelBase, INotifyPropertyChanged
    {
        private UserSettings _settings;
        private object _currentView;
        private readonly LauncherService _launcherService;
        private readonly ProcessMonitorService _processMonitor;

        public AccountOverviewViewModel AccountOverviewViewModel { get; set; }
        public LandingPageViewModel LandingPageViewModel { get; set; }
        public SettingsViewModel SettingsViewModel { get; set; }
        public MassAccountHandlerViewModel MassAccountHandler { get; set; }

        public object CurrentView
        {
            get => _currentView;
            set
            {
                _currentView = value;
                OnPropertyChanged();
            }
        }

        public ICommand ChangeViewCommand { get; }

        public MainWindowViewModel(Window window, UserSettings settings, LauncherService launcher, ProcessMonitorService monitor)
        {
            _settings = settings;
            _launcherService = launcher;
            _processMonitor = monitor;

            // 1. Initialize Child ViewModels
            AccountOverviewViewModel = new AccountOverviewViewModel(_settings);
            LandingPageViewModel = new LandingPageViewModel();
            
            // Pass the StorageProvider for the "Browse" buttons in settings
            SettingsViewModel = new SettingsViewModel(window.StorageProvider, _settings);
            
            // Pass all dependencies to the Mass Handler
            MassAccountHandler = new MassAccountHandlerViewModel(
                AccountOverviewViewModel, 
                _settings, 
                _launcherService, 
                _processMonitor);

            // --- FIX: Pass the MassAccountHandler reference to the LauncherService ---
            // This allows the HTTP server to update the correct model when a break starts
            _launcherService.SetMassAccountHandler(MassAccountHandler);

            // 2. Start the background monitoring
            _processMonitor.InitializeMonitoring(MassAccountHandler.AccountProcesses);
            
            _launcherService.Start();

            _currentView = LandingPageViewModel;
            ChangeViewCommand = new RelayCommand<string>(ExecuteChangeView);
            
            System.Console.WriteLine("[MainWindowViewModel] Initialization complete");
        }

        private void ExecuteChangeView(string? viewName)
        {
            CurrentView = viewName switch
            {
                "AccountOverview" => AccountOverviewViewModel,
                "Settings" => SettingsViewModel,
                "MassAccountHandler" => MassAccountHandler,
                _ => LandingPageViewModel
            };
        }

        public new event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
