using System.Diagnostics;
using System.Windows;
using Arcanum.Core;
namespace Arcanum.Launcher;
public partial class SettingsWindow : Window
{
    public Settings Result { get; private set; }
    public SettingsWindow(Settings settings)
    { InitializeComponent(); Result = settings with { }; OwnerBox.Text = settings.Owner; RepoBox.Text = settings.Repository; PathBox.Text = settings.GameDirectory; AutoBox.IsChecked = settings.AutoCheck; }
    private void Save(object sender, RoutedEventArgs e)
    {
        try
        {
            string path = PathBox.Text.Trim();
            if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\") || Directory.GetParent(path) == null) throw new IOException("Choisissez un sous-dossier local avec un chemin absolu.");
            string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            string launcher = Path.Combine(MainViewModel.Root, "Launcher");
            if (full.Equals(MainViewModel.Root, StringComparison.OrdinalIgnoreCase) || full.StartsWith(launcher, StringComparison.OrdinalIgnoreCase)) throw new IOException("Le jeu doit être installé séparément du launcher.");
            Installer.RejectLinks(full);
            if (Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any() && !File.Exists(Path.Combine(full, Installer.Marker))) throw new IOException("Ce dossier contient déjà des fichiers. Choisissez un dossier vide ou une installation gérée par Arcanum Launcher.");
            Result = Result with { Owner = OwnerBox.Text.Trim(), Repository = RepoBox.Text.Trim(), GameDirectory = full, AutoCheck = AutoBox.IsChecked == true };
            DialogResult = true;
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Paramètres invalides"); }
    }
    private void OpenFolder(string path)
    { try { Directory.CreateDirectory(path); Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(this, ex.Message); } }
    private void OpenGame(object sender, RoutedEventArgs e) => OpenFolder(Result.GameDirectory);
    private void OpenLogs(object sender, RoutedEventArgs e) => OpenFolder(Path.Combine(MainViewModel.Root, "Logs"));
}
