using Arcanum.Core;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

int passed = 0;
async Task Test(string name, Func<Task> run)
{ await run(); Console.WriteLine("PASS " + name); passed++; }
void Assert(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
async Task Throws(Func<Task> run)
{ try { await run(); } catch { return; } throw new Exception("Expected failure"); }
string root = Path.Combine(Path.GetTempPath(), "arcanum-tests-" + Guid.NewGuid()); Directory.CreateDirectory(root);
byte[] Zip(params (string path, string content)[] files)
{ using var memory = new MemoryStream(); using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true)) foreach (var item in files) { using var writer = new StreamWriter(zip.CreateEntry(item.path).Open()); writer.Write(item.content); } return memory.ToArray(); }
string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
Release R(byte[] bytes, string version = "v0.1.0") => new(version, "Notes", new("https://github.com/test/game/releases/download/" + version + "/Arcanum-Windows.zip"), bytes.Length, new("https://github.com/test/game/releases/download/" + version + "/Arcanum-Windows.zip.sha256"), "test/game");
GitHubSource Source(byte[] bytes, string? checksum = null, bool fail = false, bool truncated = false) => new(new HttpClient(new FakeHandler(request =>
{
    if (fail) throw new HttpRequestException("offline");
    byte[] body = request.RequestUri!.AbsolutePath.EndsWith(".sha256") ? Encoding.UTF8.GetBytes((checksum ?? Hash(bytes)) + "  Arcanum-Windows.zip") : truncated ? bytes[..(bytes.Length / 2)] : bytes;
    return new(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
})));
try
{
    await Test("Versions numériques / rejet bêta", async () => { Assert(GameVersion.Parse("v0.10.0").CompareTo(GameVersion.Parse("v0.9.9")) > 0); await Throws(() => Task.FromResult(GameVersion.Parse("v1.0.0-beta"))); });
    foreach (string attack in new[] { "../escape", "/absolute", "C:/evil", "folder/../../evil", "a:stream", "CON.txt", "a/../b", "foo. /x", "a\\..\\evil", ".arcanum-install.json", "saves/player.json" })
        await Test("ZIP refuse " + attack, () => Throws(() => Task.FromResult(Installer.SafePath(root, attack))));
    string game = Path.Combine(root, "Game"); var installer = new Installer(game, () => false);
    byte[] v1 = Zip(("Arcanum.exe", "dummy exe v1"), ("Arcanum.pck", "pck"), ("obsolete.dll", "old"));
    await Test("Premier démarrage sans jeu", () => { Assert(!installer.IsInstalled); return Task.CompletedTask; });
    await Test("Installation par téléchargement simulé", async () => { await installer.UpdateAsync(R(v1), Source(v1), null, default); Assert(installer.IsInstalled); Assert(installer.Current!.Version == "v0.1.0"); });
    await Test("Réouverture et persistance", () => { Assert(new Installer(game, () => false).Current!.Version == "v0.1.0"); return Task.CompletedTask; });
    byte[] v2 = Zip(("Arcanum.exe", "dummy exe v2"), ("Arcanum.pck", "new pck"));
    File.WriteAllText(Path.Combine(game, "my-profile.json"), "keep me");
    await Test("Mise à jour / sauvegarde / fichiers personnels", async () => { await installer.UpdateAsync(R(v2, "v0.1.1"), Source(v2), null, default); Assert(File.ReadAllText(Path.Combine(game, "my-profile.json")) == "keep me"); Assert(!File.Exists(Path.Combine(game, "obsolete.dll"))); Assert(Directory.Exists(game + ".previous")); Assert(installer.Current!.Version == "v0.1.1"); });
    await Test("Déjà à jour / pas de régression", () => { Assert(GameVersion.Parse("v0.1.1").CompareTo(GameVersion.Parse(installer.Current!.Version)) == 0); return Task.CompletedTask; });
    await Test("Hors ligne conserve le jeu", async () => { await Throws(() => installer.UpdateAsync(R(v2), Source(v2, fail: true), null, default)); Assert(installer.IsInstalled); });
    await Test("Téléchargement tronqué", () => Throws(() => installer.UpdateAsync(R(v2), Source(v2, truncated: true), null, default)));
    await Test("Mauvaise empreinte conserve v2", async () => { await Throws(() => installer.UpdateAsync(R(v1), Source(v1, new string('0', 64)), null, default)); Assert(File.ReadAllText(Path.Combine(game, "Arcanum.exe")) == "dummy exe v2"); });
    byte[] invalid = Encoding.UTF8.GetBytes("not a zip");
    await Test("Archive invalide", () => Throws(() => installer.UpdateAsync(R(invalid), Source(invalid), null, default)));
    await Test("Jeu ouvert refuse mise à jour", () => Throws(() => new Installer(game, () => true).UpdateAsync(R(v2), Source(v2), null, default)));
    await Test("Annulation préserve le jeu", async () => { using var ct = new CancellationTokenSource(); ct.Cancel(); await Throws(() => installer.UpdateAsync(R(v2), Source(v2), null, ct.Token)); Assert(installer.IsInstalled); });
    await Test("Collision fichier personnel refuse remplacement", async () => { byte[] collision = Zip(("Arcanum.exe", "v3"), ("my-profile.json", "overwrite")); await Throws(() => installer.UpdateAsync(R(collision), Source(collision), null, default)); Assert(File.ReadAllText(Path.Combine(game, "my-profile.json")) == "keep me"); });
    await Test("Récupération après interruption entre renommages", () => { Directory.Delete(game + ".previous", true); Directory.Move(game, game + ".previous"); installer.Recover(); Assert(installer.IsInstalled); return Task.CompletedTask; });
    await Test("Verrou inter-instance", async () => { using var guard = installer.AcquireLock(); await Throws(() => installer.UpdateAsync(R(v2), Source(v2), null, default)); });
    await Test("Pas d’adoption dossier personnel", async () => { string foreign = Path.Combine(root, "Foreign"); Directory.CreateDirectory(foreign); File.WriteAllText(Path.Combine(foreign, "important.txt"), "important"); await Throws(() => new Installer(foreign, () => false).UpdateAsync(R(v1), Source(v1), null, default)); Assert(File.Exists(Path.Combine(foreign, "important.txt"))); });
    await Test("API latest stable / notes / assets", async () =>
    {
        var payload = new { tag_name = "v0.2.0", draft = false, prerelease = false, body = "Une nouvelle aventure", assets = new[] { new { name = "Arcanum-Windows.zip", size = 123, browser_download_url = "https://github.com/test/game/releases/download/v0.2.0/Arcanum-Windows.zip" }, new { name = "Arcanum-Windows.zip.sha256", size = 64, browser_download_url = "https://github.com/test/game/releases/download/v0.2.0/Arcanum-Windows.zip.sha256" } } };
        var api = new GitHubSource(new HttpClient(new FakeHandler(_ => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(payload)) })));
        var result = await api.LatestAsync(new Settings { Owner = "test", Repository = "game" }, default); Assert(result.Version == "v0.2.0"); Assert(result.Notes.Contains("aventure"));
    });
    foreach (var code in new[] { HttpStatusCode.NotFound, HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests })
        await Test("API erreur " + code, () => Throws(() => new GitHubSource(new HttpClient(new FakeHandler(_ => new(code)))).LatestAsync(new Settings { Owner = "test", Repository = "game" }, default)));
    await Test("Espace disque insuffisant", () => Throws(() => { Installer.RequireSpace(game, 1L << 60); return Task.CompletedTask; }));
    await Test("ZIP malveillant via transaction complète", async () => { byte[] attack = Zip(("Arcanum.exe", "bad"), ("../escape.txt", "evil")); await Throws(() => installer.UpdateAsync(R(attack), Source(attack), null, default)); Assert(!File.Exists(Path.Combine(root, "escape.txt"))); Assert(installer.IsInstalled); });
    Console.WriteLine($"SUCCESS: {passed} tests");
}
finally { Directory.Delete(root, true); }
sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{ protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(responder(request)); } }
