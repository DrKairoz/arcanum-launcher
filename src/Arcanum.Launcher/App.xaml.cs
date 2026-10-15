using System.Windows;
namespace Arcanum.Launcher;
public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new Mutex(true, @"Local\ArcanumLauncher.V1", out bool created);
        if (!created) { MessageBox.Show("Arcanum Launcher est déjà ouvert."); Shutdown(); return; }
        var window = new MainWindow(); MainWindow = window; window.Show();
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}
