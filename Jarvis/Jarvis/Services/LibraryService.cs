using FuzzySharp;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Jarvis.Services;

public record Target(string Display, string Key, string Path, bool IsSong);

/// <summary>
/// Singleton service, manages music library
/// </summary>
public class LibraryService
{
    const int MinScore = 45;
    static readonly EnumerationOptions Dirs = new() { IgnoreInaccessible = true };
    static readonly EnumerationOptions Mp3s = new() { RecurseSubdirectories = true, IgnoreInaccessible = true };

    readonly List<Target> _targets = [];
    public IReadOnlyList<string> ArtistNames { get; }
    private readonly MediaService _mediaService;
    public LibraryService(IOptions<AppSettings> settings, MediaService mediaService)
    {
        _mediaService = mediaService;
        string rootDirectory = settings.Value.RootDirectory;
        var artists = Directory.GetDirectories(rootDirectory, "*", Dirs);
        ArtistNames = artists.Select(a => Path.GetFileName(a)!).ToList();

        // Artists first, so on a tie "Led Zeppelin" the artist beats the self-titled album
        foreach (var artist in artists)
        {
            _targets.Add(new(Path.GetFileName(artist)!, Normalize(Path.GetFileName(artist)!), artist, false));
            foreach (var album in Directory.GetDirectories(artist, "*", Dirs))
            {
                _targets.Add(new($"{Path.GetFileName(artist)} – {Path.GetFileName(album)}", Normalize(Path.GetFileName(album)!), album, false));
                foreach (var song in Directory.GetFiles(album, "*.mp3", Mp3s))
                {
                    string songName = Path.GetFileName(Normalize(song));
                    _targets.Add(new(songName, songName, song, true));
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
        Stopwatch sw = Stopwatch.StartNew();
        var q = Normalize(query);
        var best = _targets
            .Select(t => (t, Set: Fuzz.TokenSetRatio(q, t.Key), Ratio: Fuzz.Ratio(q, t.Key)))
            .OrderByDescending(x => x.Set)
            .ThenByDescending(x => x.Ratio)
            .First();

        sw.Stop();
        Console.WriteLine($"[Match] '{query}' -> {best.t.Display} ({best.Set}/{best.Ratio}) in {sw.ElapsedMilliseconds}ms");
        return best.Set >= MinScore ? best.t : null;
    }

    public List<string> Songs(Target t, bool shuffle)
    {
        string[] files;
        if (t.IsSong)
        {
            files = [t.Path];
        }
        else
        {
            files = Directory.GetFiles(t.Path, "*.mp3", Mp3s);
        }
        return shuffle ? files.Shuffle().ToList() : InOrder(files);
    }

    // Album folder, then disc, then track number from the MP3 tags; file name as fallback
    static List<string> InOrder(IEnumerable<string> files) =>
        files.Select(f =>
        {
            using var tag = TagLib.File.Create(f);
            return (f, dir: Path.GetDirectoryName(f), disc: tag.Tag.Disc, track: tag.Tag.Track);
        })
        .OrderBy(x => x.dir).ThenBy(x => x.disc).ThenBy(x => x.track).ThenBy(x => x.f)
        .Select(x => x.f).ToList();
}
