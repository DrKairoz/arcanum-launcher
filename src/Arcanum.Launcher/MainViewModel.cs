using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Arcanum.Core;
namespace Arcanum.Launcher;
public sealed class RelayCommand(Action action, Func<bool>? can = null) : ICommand
{
    public bool CanExecute(object? parameter) => can?.Invoke() ?? true;
    public void Execute(object? parameter) => action();
    public event EventHandler? CanExecuteChanged;
    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
public sealed class MainViewModel : INotifyPropertyChanged
{
    public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Arcanum");
    private static readonly string Config = Path.Combine(Root, "Launcher", "Configuration", "settings.json");
    private readonly GitHubSource source = new(GitHubSource.CreateClient());
    private Installer installer = null!;
    private Release? release;
    private CancellationTokenSource? cancellation;
    public Settings Settings { get; private set; } = new();
    public bool Busy { get; private set; }
    public bool NotBusy => !Busy;
    public string InstalledLabel { get; private set; } = "Jeu non installé";
    public string LatestLabel { get; private set; } = "Bienvenue à la taverne";
    public string Notes { get; private set; } = "Configurez le dépôt GitHub public dans les paramètres pour installer Arcanum. Une fois installé, le jeu reste accessible hors ligne.";
    public string Status { get; private set; } = "Prêt";
    public string LastCheck { get; private set; } = "Pas encore vérifié";
    public string ProgressLabel { get; private set; } = "";
    public double Percent { get; private set; }
    public string? BackgroundImage => File.Exists(Path.Combine(AppContext.BaseDirectory, "Assets", "background.png")) ? Path.Combine(AppContext.BaseDirectory, "Assets", "background.png") : null;
    private bool installed;
    private bool updateAvailable;
    public string ActionLabel => installed ? "Jouer" : "Installer Arcanum";
    public RelayCommand PrimaryCommand { get; }
    public RelayCommand CheckCommand { get; }
    public RelayCommand CancelCommand { get; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public MainViewModel()
    {
        PrimaryCommand = new(() => _ = RunAsync(PrimaryAsync), () => !Busy);
        CheckCommand = new(() => _ = RunAsync(CheckAsync), () => !Busy);
        CancelCommand = new(Cancel, () => Busy);
    }
    private void Refresh()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        PrimaryCommand.Refresh(); CheckCommand.Refresh(); CancelCommand.Refresh();
    }
    public async Task InitializeAsync()
    {
        await RunAsync(async ct =>
        {
            Settings = JsonFiles.Read<Settings>(Config) ?? JsonFiles.Read<Settings>(Path.Combine(AppContext.BaseDirectory, "Configuration", "defaults.json")) ?? new();
            ConfigureInstaller();
            if (!IsRunning()) { using var guard = installer.AcquireLock(); installer.Recover(); }
            ReadLocal();
            if (Settings.AutoCheck) await CheckAsync(ct);
        });
    }
    private static bool IsRunning()
    {
        var processes = Process.GetProcessesByName("Arcanum");
        bool running = processes.Length > 0; foreach (var process in processes) process.Dispose(); return running;
    }
    private void ConfigureInstaller() => installer = new(Settings.GameDirectory, IsRunning);
    private void ReadLocal()
    {
        installed = installer.IsInstalled;
        InstalledLabel = installed ? "Version installée · " + installer.Current!.Version : "Jeu non installé";
        Refresh();
    }
    public void ApplySettings(Settings settings)
    {
        try
        {
            JsonFiles.Write(Config, settings); Settings = settings; release = null; updateAvailable = false; ConfigureInstaller(); ReadLocal();
            LatestLabel = "Configuration enregistrée"; Notes = "Cliquez sur Vérifier pour consulter la publication du dépôt configuré."; Status = "Paramètres enregistrés."; Refresh();
        }
        catch (Exception ex) { Error(ex); }
    }
    private async Task CheckAsync(CancellationToken ct)
    {
        Status = "Recherche de la dernière version…"; release = null; updateAvailable = false; Refresh();
        release = await source.LatestAsync(Settings, ct);
        LatestLabel = "Arcanum " + release.Version; Notes = release.Notes;
        LastCheck = "Vérifié à " + DateTime.Now.ToString("HH:mm");
        ReadLocal();
        if (installed && installer.Current!.Repository != release.Repository) throw new IOException("Le dépôt configuré diffère de celui du jeu installé. Choisissez un nouveau dossier pour changer de dépôt.");
        updateAvailable = !installed || GameVersion.Parse(release.Version).CompareTo(GameVersion.Parse(installer.Current!.Version)) > 0;
        Status = updateAvailable ? "Nouvelle version disponible." : "Votre jeu est à jour."; Refresh();
        if (installed && updateAvailable) await UpdateAsync(ct);
    }
    private async Task UpdateAsync(CancellationToken ct)
    {
        if (release == null) throw new IOException("Vérifiez d’abord la publication.");
        var progress = new Progress<TransferProgress>(p => { Status = p.Phase; Percent = p.Percent; ProgressLabel = $"{p.Percent:F0} %" + (p.BytesPerSecond > 0 ? $" · {p.BytesPerSecond / 1048576:F1} Mo/s" : ""); Refresh(); });
        await Task.Run(() => installer.UpdateAsync(release, source, progress, ct), ct);
        updateAvailable = false; ReadLocal(); Status = "Arcanum est prêt. Bonne partie !"; Percent = 100;
    }
    private async Task PrimaryAsync(CancellationToken ct)
    {
        if (!installed)
        {
            if (release == null) await CheckAsync(ct);
            await UpdateAsync(ct); return;
        }
        if (IsRunning()) { Status = "Arcanum est déjà ouvert."; return; }
        // Launch stays available after network/update errors, including an older installed version.
        Process.Start(new ProcessStartInfo(Path.Combine(Settings.GameDirectory, "Arcanum.exe")) { WorkingDirectory = Settings.GameDirectory, UseShellExecute = true });
        Status = "Arcanum lancé.";
    }
    private async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        if (Busy) return;
        Busy = true; cancellation = new(); Percent = 0; ProgressLabel = ""; Refresh();
        try { await operation(cancellation.Token); }
        catch (OperationCanceledException) { Status = cancellation.IsCancellationRequested ? "Opération annulée. L’installation existante est conservée." : "Délai réseau dépassé. Réessayez ; le jeu installé reste accessible."; }
        catch (Exception ex) { Error(ex); }
        finally { Busy = false; cancellation.Dispose(); cancellation = null; Refresh(); }
    }
    private void Error(Exception ex)
    {
        Status = ex.Message + (installed ? " Le jeu installé reste accessible via Jouer." : " Une connexion et une release valide sont nécessaires pour installer.");
        try { Directory.CreateDirectory(Path.Combine(Root, "Logs")); File.AppendAllText(Path.Combine(Root, "Logs", DateTime.Now.ToString("yyyy-MM") + ".log"), DateTimeOffset.Now + " " + ex + Environment.NewLine); } catch { }
        Refresh();
    }
    public void Cancel() => cancellation?.Cancel();
}
