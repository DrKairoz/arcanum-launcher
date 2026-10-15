using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Arcanum.Core;
public sealed class GitHubSource(HttpClient http)
{
    public const string AssetName = "Arcanum-Windows.zip";
    public static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ArcanumLauncher/1.0.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
    public async Task<Release> LatestAsync(Settings settings, CancellationToken ct)
    {
        if (!Regex.IsMatch(settings.Owner, @"^[A-Za-z0-9-]+$") || !Regex.IsMatch(settings.Repository, @"^[A-Za-z0-9_.-]+$"))
            throw new InvalidOperationException("Renseignez le propriétaire et le dépôt GitHub public dans les paramètres.");
        string repo = settings.Owner + "/" + settings.Repository;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await http.GetAsync("https://api.github.com/repos/" + repo + "/releases/latest", timeout.Token);
        if (response.StatusCode == HttpStatusCode.NotFound) throw new IOException("Dépôt introuvable ou aucune release stable publiée.");
        if ((int)response.StatusCode is 403 or 429) throw new IOException("GitHub limite les requêtes ou refuse l’accès. Réessayez plus tard ; le jeu installé reste accessible.");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        var root = doc.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) throw new InvalidDataException("La publication n’est pas stable.");
        string tag = root.GetProperty("tag_name").GetString()!; GameVersion.Parse(tag);
        var assets = root.GetProperty("assets").EnumerateArray().ToArray();
        JsonElement Asset(string name) => assets.SingleOrDefault(a => a.GetProperty("name").GetString() == name) is var a && a.ValueKind != JsonValueKind.Undefined ? a : throw new InvalidDataException("Fichier manquant : " + name);
        var archive = Asset(AssetName); var checksum = Asset(AssetName + ".sha256");
        Uri Url(JsonElement item)
        {
            var uri = new Uri(item.GetProperty("browser_download_url").GetString()!);
            if (uri.Scheme != "https" || uri.Host != "github.com" || !uri.AbsolutePath.StartsWith("/" + repo + "/releases/download/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Adresse de téléchargement GitHub inattendue.");
            return uri;
        }
        long size = archive.GetProperty("size").GetInt64();
        if (size <= 0 || size > 8L * 1024 * 1024 * 1024) throw new InvalidDataException("Taille d’archive invalide (maximum 8 Gio).");
        return new(tag, root.GetProperty("body").GetString() ?? "Aucune note de mise à jour.", Url(archive), size, Url(checksum), repo);
    }
    public async Task<string> ChecksumAsync(Release release, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await http.GetAsync(release.Checksum, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var memory = new MemoryStream(); byte[] buffer = new byte[4096];
        int count;
        while ((count = await stream.ReadAsync(buffer, timeout.Token)) != 0)
        { if (memory.Length + count > 4096) throw new InvalidDataException("Fichier SHA-256 trop volumineux."); await memory.WriteAsync(buffer.AsMemory(0, count), timeout.Token); }
        string text = System.Text.Encoding.UTF8.GetString(memory.ToArray()).Trim().TrimStart('\uFEFF');
        var match = Regex.Match(text, @"\A([a-fA-F0-9]{64})(?:[ \t]+\*?Arcanum-Windows\.zip)?\z");
        if (!match.Success) throw new InvalidDataException("Fichier SHA-256 invalide.");
        return match.Groups[1].Value;
    }
    public async Task DownloadAsync(Release release, string destination, IProgress<TransferProgress>? progress, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(45));
        using var response = await http.GetAsync(release.Archive, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long length && length != release.Size) throw new InvalidDataException("Taille de téléchargement inattendue.");
        using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true);
        byte[] buffer = new byte[131072]; long total = 0; var watch = Stopwatch.StartNew(); long lastReport = 0;
        while (true)
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(45));
            int count = await input.ReadAsync(buffer, timeout.Token); if (count == 0) break;
            total += count; if (total > release.Size) throw new InvalidDataException("Téléchargement plus volumineux qu’annoncé.");
            await output.WriteAsync(buffer.AsMemory(0, count), ct);
            if (watch.ElapsedMilliseconds - lastReport >= 100 || total == release.Size)
            { progress?.Report(new("Téléchargement", total * 100.0 / release.Size, total / Math.Max(0.001, watch.Elapsed.TotalSeconds))); lastReport = watch.ElapsedMilliseconds; }
        }
        if (total != release.Size) throw new IOException("Téléchargement interrompu. Réessayez pour recommencer.");
        await output.FlushAsync(ct);
    }
}
