using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using JagexAccountSwitcher.ViewModels;

namespace JagexAccountSwitcher.Views;

public partial class MassAccountHandler : UserControl
{
    // Parameterless constructor for XAML instantiation
    public MassAccountHandler()
    {
        InitializeComponent();
    }

    // Constructor with ViewModel for programmatic instantiation
    public MassAccountHandler(MassAccountHandlerViewModel massAccountHandlerViewModel)
    {
        InitializeComponent();
        DataContext = massAccountHandlerViewModel;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}