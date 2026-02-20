using Avalonia.Controls;
using SettingsEditor.ViewModels;

namespace SettingsEditor;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
