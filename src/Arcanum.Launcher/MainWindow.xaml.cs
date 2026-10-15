using System.ComponentModel;
using System.Windows;
namespace Arcanum.Launcher;
public partial class MainWindow : Window
{
    private readonly MainViewModel vm = new();
    public MainWindow()
    {
        InitializeComponent(); DataContext = vm;
        Loaded += async (_, _) => await vm.InitializeAsync();
        Closing += OnClosing;
    }
    private void SettingsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(vm.Settings) { Owner = this };
        if (dialog.ShowDialog() == true) vm.ApplySettings(dialog.Result);
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (vm.Busy) { e.Cancel = true; vm.Cancel(); MessageBox.Show(this, "Annulation demandée. Attendez la fin de l’opération puis fermez le launcher."); }
    }
}
