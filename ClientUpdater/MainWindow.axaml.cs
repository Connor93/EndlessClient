using Avalonia.Controls;
using ClientUpdater.ViewModels;

namespace ClientUpdater;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new UpdaterViewModel();
    }
}
