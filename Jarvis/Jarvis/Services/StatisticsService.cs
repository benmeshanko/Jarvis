using Jarvis.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jarvis.Services;

/// <summary>
/// Singleton service, manages statistics of song listens
/// </summary>
public class StatisticsService(
    IServiceScopeFactory scopeFactory,
    IOptions<AppSettings> settings) : IHostedService
{
    private string? _currentSong = null;
    private DateTime _currentSongStartTime = DateTime.UtcNow;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Get existing Songs
        HashSet<SongKey> songKeys = await GetAllSongKeysAsync(dbContext, cancellationToken);

        // Index library of Songs
        string rootDirectory = settings.Value.RootDirectory;
        List<Song> songs = [];
        foreach (string songPath in Directory.GetFiles(rootDirectory, "*.mp3", new EnumerationOptions()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
        }))
        {
            TagLib.File file = TagLib.File.Create(songPath);
            Song song = new()
            {
                Artist = file.Tag.FirstAlbumArtist,
                Album = file.Tag.Album,
                Title = file.Tag.Title
            };
            if (song.Artist is not null && song.Album is not null && song.Title is not null
                && !songKeys.Contains(new SongKey(song.Artist, song.Album, song.Title)))
            {
                songs.Add(song);
            }
        }

        dbContext.AddRange(songs);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordSongAsync(string? songPath, CancellationToken cancellationToken = default)
    {
        if (_currentSong is not null)
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            TagLib.File file = TagLib.File.Create(_currentSong);
            Song song = await dbContext.Songs
                .FirstAsync(s => s.Artist == file.Tag.FirstAlbumArtist && s.Album == file.Tag.Album && s.Title == file.Tag.Title, cancellationToken);

            song.Plays++;
            song.ListenTime += DateTime.UtcNow - _currentSongStartTime;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        _currentSong = songPath;
        _currentSongStartTime = DateTime.UtcNow;
    }

    private static Task<HashSet<SongKey>> GetAllSongKeysAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        return dbContext.Songs.AsNoTracking()
            .Select(s => new SongKey(s.Artist, s.Album, s.Title))
            .ToHashSetAsync(cancellationToken);
    }

    public readonly record struct SongKey(string Artist, string Album, string Title);

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return RecordSongAsync(null, cancellationToken);
    }
}
