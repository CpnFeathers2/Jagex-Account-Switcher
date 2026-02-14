using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JagexAccountSwitcher.Model;

public class RunescapeAccount : INotifyPropertyChanged
{
    private string _accountName = string.Empty;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private bool _isActiveAccount;
    private string? _clientArguments;

    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string AccountName 
    { 
        get => _accountName; 
        set { _accountName = value; OnPropertyChanged(); } 
    }

    public string Email 
    { 
        get => _email; 
        set { _email = value; OnPropertyChanged(); } 
    }

    public string Password 
    { 
        get => _password; 
        set { _password = value; OnPropertyChanged(); } 
    }

    public string FilePath { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;

    public bool IsActiveAccount
    {
        get => _isActiveAccount;
        set { _isActiveAccount = value; OnPropertyChanged(); }
    }

    public string? ClientArguments
    {
        get => _clientArguments;
        set
        {
            _clientArguments = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasClientArguments));
        }
    }

    public bool HasClientArguments => !string.IsNullOrWhiteSpace(ClientArguments);

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
