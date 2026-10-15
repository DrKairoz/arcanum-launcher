using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Arcanum.Core;
public sealed class Installer(string gameDirectory, Func<bool> gameRunning)
{
    public const string Marker = ".arcanum-install.json";
    public const string Manifest = ".arcanum-files.json";
    public string GameDirectory { get; } = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameDirectory));
    private string Backup => GameDirectory + ".previous";
    private string Work => GameDirectory + ".staging";
    public Installed? Current => JsonFiles.Read<Installed>(Path.Combine(GameDirectory, Marker));
    public bool IsInstalled => Current != null && File.Exists(Path.Combine(GameDirectory, "Arcanum.exe"));
    public FileStream AcquireLock()
    {
        var parent = Directory.GetParent(GameDirectory) ?? throw new IOException("Choisissez un sous-dossier dédié au jeu.");
        Directory.CreateDirectory(parent.FullName);
        return new FileStream(GameDirectory + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    private void CheckClosed()
    { if (gameRunning()) throw new IOException("Fermez Arcanum avant de mettre à jour le jeu."); }
    public void Recover()
    {
        CheckClosed(); RejectLinks(GameDirectory); RejectLinks(Backup); RejectLinks(Work);
        if (!Directory.Exists(GameDirectory) && Directory.Exists(Backup))
        {
            if (!File.Exists(Path.Combine(Backup, Marker))) throw new IOException("Sauvegarde non reconnue : restauration manuelle nécessaire.");
            Directory.Move(Backup, GameDirectory);
        }
    }
    public static void RejectLinks(string path)
    {
        for (var dir = new DirectoryInfo(path); dir != null; dir = dir.Parent)
            if (dir.Exists && dir.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Les liens de dossiers ne sont pas pris en charge.");
        if (!Directory.Exists(path)) return;
        foreach (var entry in Directory.EnumerateFileSystemEntries(path))
        {
            if (File.GetAttributes(entry).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Lien détecté dans l’installation.");
            if (Directory.Exists(entry)) RejectLinks(entry);
        }
    }
    public static void RequireSpace(string path, long required)
    {
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!);
        if (drive.AvailableFreeSpace < required + 256L * 1024 * 1024) throw new IOException("Espace disque insuffisant. Libérez de l’espace puis réessayez.");
    }
    public async Task UpdateAsync(Release release, GitHubSource source, IProgress<TransferProgress>? progress, CancellationToken ct)
    {
        using var installLock = AcquireLock();
        Recover(); ValidateTarget();
        if (Current is { } current && current.Repository != release.Repository)
            throw new IOException("Cette installation appartient à un autre dépôt. Choisissez un nouveau dossier vide.");
        if (Directory.Exists(Work))
        {
            if (!File.Exists(Path.Combine(Work, ".launcher-work"))) throw new IOException("Dossier temporaire non reconnu : " + Work);
            Directory.Delete(Work, true);
        }
        Directory.CreateDirectory(Work); File.WriteAllText(Path.Combine(Work, ".launcher-work"), "Arcanum Launcher");
        try
        {
            RequireSpace(GameDirectory, release.Size);
            string archive = Path.Combine(Work, "download.zip");
            string hash = await source.ChecksumAsync(release, ct);
            await source.DownloadAsync(release, archive, progress, ct);
            await InstallArchiveAsync(archive, hash, release, Path.Combine(Work, "payload"), progress, ct);
        }
        finally { if (Directory.Exists(Work)) Directory.Delete(Work, true); }
    }
    private void ValidateTarget()
    {
        if (Directory.Exists(GameDirectory) && Directory.EnumerateFileSystemEntries(GameDirectory).Any() && Current == null)
            throw new IOException("Le dossier choisi contient des fichiers qui n’appartiennent pas au launcher. Choisissez un dossier vide.");
    }
    // Public entry for deterministic integration tests. Caller must own the installation lock.
    public async Task InstallArchiveAsync(string archive, string expectedHash, Release release, string stage, IProgress<TransferProgress>? progress, CancellationToken ct)
    {
        CheckClosed(); ValidateTarget(); RejectLinks(GameDirectory); RejectLinks(Backup);
        GameVersion.Parse(release.Version);
        progress?.Report(new("Vérification SHA-256", 0));
        using (var input = File.OpenRead(archive))
        {
            string actual = Convert.ToHexString(await SHA256.HashDataAsync(input, ct));
            if (!actual.Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Empreinte SHA-256 incorrecte. Aucun fichier du jeu n’a été remplacé.");
        }
        if (Directory.Exists(stage)) throw new IOException("Dossier de préparation déjà présent.");
        Directory.CreateDirectory(stage);
        using (var zip = ZipFile.OpenRead(archive))
        {
            if (zip.Entries.Count > 100000) throw new InvalidDataException("Archive contenant trop de fichiers.");
            long total = 0; var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in zip.Entries)
            {
                SafePath(stage, entry.FullName);
                if (!targets.Add(entry.FullName.TrimEnd('/'))) throw new InvalidDataException("Chemin ZIP en double.");
                if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("Lien symbolique interdit dans le ZIP.");
                total = checked(total + entry.Length);
                if (total > 24L * 1024 * 1024 * 1024) throw new InvalidDataException("Archive décompressée trop volumineuse (maximum 24 Gio).");
            }
            long personalSize = Directory.Exists(GameDirectory) ? Directory.EnumerateFiles(GameDirectory, "*", SearchOption.AllDirectories).Sum(p => new FileInfo(p).Length) : 0;
            RequireSpace(stage, checked(total + personalSize));
            long extracted = 0;
            foreach (var entry in zip.Entries)
            {
                ct.ThrowIfCancellationRequested(); string target = SafePath(stage, entry.FullName);
                if (entry.FullName.EndsWith('/')) { Directory.CreateDirectory(target); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var input = entry.Open(); using var output = new FileStream(target, FileMode.CreateNew);
                await input.CopyToAsync(output, ct); extracted += entry.Length;
                progress?.Report(new("Extraction", total == 0 ? 100 : extracted * 100.0 / total));
            }
        }
        if (!File.Exists(Path.Combine(stage, "Arcanum.exe"))) throw new InvalidDataException("Arcanum.exe doit être à la racine du ZIP.");
        var owned = Directory.EnumerateFiles(stage, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(stage, p)).ToArray();
        if (Directory.Exists(GameDirectory))
        {
            var previous = JsonFiles.Read<string[]>(Path.Combine(GameDirectory, Manifest)) ?? throw new InvalidDataException("Manifest de l’installation absent.");
            var tracked = previous.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.EnumerateFiles(GameDirectory, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(GameDirectory, file);
                if (relative == Marker || relative == Manifest) continue;
                if (tracked.Contains(relative) && !IsPersonal(relative)) continue;
                string target = Path.Combine(stage, relative);
                if (File.Exists(target)) throw new IOException("La mise à jour entrerait en conflit avec un fichier personnel : " + relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target);
            }
        }
        JsonFiles.Write(Path.Combine(stage, Manifest), owned);
        JsonFiles.Write(Path.Combine(stage, Marker), new Installed(release.Version, release.Repository, DateTimeOffset.UtcNow));
        ct.ThrowIfCancellationRequested(); CheckClosed();
        progress?.Report(new("Installation sécurisée — ne fermez pas le launcher", 100));
        if (Directory.Exists(Backup))
        {
            if (!File.Exists(Path.Combine(Backup, Marker))) throw new IOException("Ancienne sauvegarde non reconnue.");
            Directory.Delete(Backup, true);
        }
        bool moved = false;
        try
        {
            if (Directory.Exists(GameDirectory)) { Directory.Move(GameDirectory, Backup); moved = true; }
            Directory.Move(stage, GameDirectory);
        }
        catch
        {
            if (moved && !Directory.Exists(GameDirectory)) Directory.Move(Backup, GameDirectory);
            throw;
        }
    }
    public static bool IsPersonal(string path)
    {
        string normalized = path.Replace('\\', '/');
        return Regex.IsMatch(normalized, @"^(saves?|profiles?|user_data|config(?:uration)?)(/|\.)", RegexOptions.IgnoreCase)
            || Regex.IsMatch(normalized, @"(^|/)(settings|savegame|preferences)\.(json|cfg|ini|dat)$", RegexOptions.IgnoreCase);
    }
    public static string SafePath(string root, string name)
    {
        string normalized = name.Replace('\\', '/');
        if (string.IsNullOrEmpty(normalized) || normalized.StartsWith('/') || normalized.Contains(':') || normalized.Any(char.IsControl)) throw new InvalidDataException("Chemin ZIP interdit.");
        foreach (string part in normalized.TrimEnd('/').Split('/'))
            if (part is "" or "." or ".." || part.EndsWith('.') || part.EndsWith(' ') || part.IndexOfAny(['<', '>', '"', '|', '?', '*']) >= 0 || Regex.IsMatch(part, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", RegexOptions.IgnoreCase))
                throw new InvalidDataException("Chemin ZIP non sûr : " + name);
        if (normalized.StartsWith(".arcanum-", StringComparison.OrdinalIgnoreCase) || IsPersonal(normalized)) throw new InvalidDataException("Chemin réservé aux données locales : " + name);
        string full = Path.GetFullPath(Path.Combine(root, normalized));
        if (!full.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Le ZIP tente de sortir du dossier.");
        return full;
    }
}
