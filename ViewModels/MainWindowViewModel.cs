using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using JagexAccountSwitcher.Model;
using System.Collections.ObjectModel;
using JagexAccountSwitcher.Views;

namespace JagexAccountSwitcher.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private UserSettings _settings;
        private object _currentView;
        private readonly Dictionary<string, object> _viewInstances;
        public AccountOverviewViewModel AccountOverviewViewModel { get; set; }
        public LandingPageViewModel LandingPageViewModel { get; set; }
        public SettingsViewModel SettingsViewModel { get; set; }
        public MassAccountHandlerViewModel MassAccountHandlerViewModel { get; set; }
	public ObservableCollection<RunescapeAccount> Accounts { get; } = new();
	public AccountOverviewViewModel? AccountOverview { get; private set; }

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

        public MainWindowViewModel(Window window)
        {
            _settings = new UserSettings();
            _settings.LoadFromFile();
            // Set default view
            AccountOverviewViewModel = new AccountOverviewViewModel(_settings);
            LandingPageViewModel = new LandingPageViewModel();
            SettingsViewModel = new SettingsViewModel(window.StorageProvider, _settings);
            MassAccountHandlerViewModel = new MassAccountHandlerViewModel(AccountOverviewViewModel, _settings, Accounts);
            ChangeViewCommand = new RelayCommand<string>(ChangeView);
            _viewInstances = new Dictionary<string, object>();
            ChangeView("LandingPage");
        }

        private void ChangeView(string viewName)
        {
            if (!_viewInstances.TryGetValue(viewName, out var view))
{
    switch (viewName)
    {
        case "AccountOverview":
    	    var overviewVm = new AccountOverviewViewModel(_settings);
    	    AccountOverview = overviewVm;
    	    view = new AccountOverview(overviewVm);
    	    break;
        case "LandingPage":
            view = new LandingPage();
            break;
        case "Guide":
            view = new Guide();
            break;
        case "Settings":
            view = new Settings(SettingsViewModel);
            break;
        case "MassAccountHandler":
            view = new MassAccountHandler(MassAccountHandlerViewModel);
            break;
        default:
            view = new LandingPage();
            break;
    }

    _viewInstances[viewName] = view;
}

            CurrentView = view;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}