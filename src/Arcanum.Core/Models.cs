using System.Text.Json;
using System.Text.RegularExpressions;

namespace Arcanum.Core;

public sealed record Settings
{
    public string Owner { get; set; } = "";
    public string Repository { get; set; } = "";
    public string GameDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Arcanum", "Game");
    public bool AutoCheck { get; set; } = true;
    public string Channel { get; set; } = "stable";
    public string Language { get; set; } = "fr";
}
public sealed record Installed(string Version, string Repository, DateTimeOffset InstalledAt);
public sealed record Release(string Version, string Notes, Uri Archive, long Size, Uri Checksum, string Repository);
public sealed record TransferProgress(string Phase, double Percent, double BytesPerSecond = 0);
public readonly record struct GameVersion(int Major, int Minor, int Patch) : IComparable<GameVersion>
{
    public static GameVersion Parse(string value)
    {
        var m = Regex.Match(value, @"^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:\+[0-9A-Za-z.-]+)?$");
        if (!m.Success) throw new InvalidDataException("Version stable invalide : " + value + " (attendu : v1.2.3).");
        return new(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
    }
    public int CompareTo(GameVersion other)
    {
        int c = Major.CompareTo(other.Major); if (c != 0) return c;
        c = Minor.CompareTo(other.Minor); return c != 0 ? c : Patch.CompareTo(other.Patch);
    }
}
public static class JsonFiles
{
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static T? Read<T>(string path) => File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) : default;
    public static void Write<T>(string path, T data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, data, Options); stream.Flush(true); }
        File.Move(temp, path, true);
    }
}
