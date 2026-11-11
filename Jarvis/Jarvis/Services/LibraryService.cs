using FuzzySharp;
using Microsoft.Extensions.Options;

namespace Jarvis.Services;

public enum TargetType { Artist, Album, Song }
public record Target(string Display, string Key, string Path, TargetType TargetType);

/// <summary>
/// Singleton service, manages music library
/// </summary>
public class LibraryService
{
    readonly List<Target> _targets = [];
    public LibraryService(IOptions<AppSettings> settings)
    {
        EnumerationOptions options = new() { IgnoreInaccessible = true };

        // Note - has implicit artist/album/song structure
        string rootDirectory = settings.Value.RootDirectory;
        foreach (string artistPath in Directory.GetDirectories(rootDirectory, "*", options))
        {
            string artistName = Path.GetFileName(artistPath);
            _targets.Add(new(artistName, Normalize(artistName), artistPath, TargetType.Artist));
            foreach (var albumPath in Directory.GetDirectories(artistPath, "*", options))
            {
                string albumName = Path.GetFileName(albumPath);
                _targets.Add(new($"{artistName} – {albumName}", Normalize(albumName), albumPath, TargetType.Album));
                foreach (var songPath in Directory.GetFiles(albumPath, "*.mp3", options))
                {
                    string songName = Path.GetFileName(Normalize(songPath));
                    _targets.Add(new(songName, songName, songPath, TargetType.Song));
                }
            }
        }
    }

    static string Normalize(string s)
    {
        s = s.ToLowerInvariant().Replace("'", "").Replace("’", "").Replace("&", "and");
        if (s.EndsWith(", the")) s = s[..^5];
        if (s.StartsWith("the ")) s = s[4..];
        return s;
    }

    public Target? Find(string query)
    {
        const int MIN_SCORE = 45;
        string normalized = Normalize(query);
        var best = _targets
            .Select(t => (t, Set: Fuzz.TokenSetRatio(normalized, t.Key), Ratio: Fuzz.Ratio(normalized, t.Key)))
            .OrderByDescending(x => x.Set)
            .ThenByDescending(x => x.Ratio)
            .First();
        return best.Set >= MIN_SCORE ? best.t : null;
    }

    public List<string> Songs(Target target, bool shuffle)
    {
        string[] files = target.TargetType is TargetType.Song
            ? [target.Path]
            : Directory.GetFiles(target.Path, "*.mp3", new EnumerationOptions() { RecurseSubdirectories = true, IgnoreInaccessible = true });
        return shuffle ? files.Shuffle().ToList() : InOrder(files);
    }

    // Album folder, then disc, then track number from the MP3 tags; file name as fallback
    static List<string> InOrder(IEnumerable<string> files) =>
        files.Select(file =>
        {
            using var tag = TagLib.File.Create(file);
            return (File: file, Directory: Path.GetDirectoryName(file), tag.Tag.Track);
        })
        .OrderBy(x => x.Directory).ThenBy(x => x.Track).ThenBy(x => x.File)
        .Select(x => x.File)
        .ToList();
}
