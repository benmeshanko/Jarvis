using LibVLCSharp.Shared;

namespace Jarvis.Services;

/// <summary>
/// Singleton service, manages playing music
/// </summary>
public class MediaService
{
    private readonly LibVLC _vlc;
    private MediaPlayer? _player = null;
    private int _volume = 6;
    private Queue<string> _queue = [];
    private bool _isSongActive = false;

    public MediaService()
    {
        Core.Initialize();
        _vlc = new();

        _queue = new(GetSongFiles(@"C:\ben\music").Shuffle());
        DequeuePlay();
    }

    private void DequeuePlay()
    {
        if (_isSongActive)
        {
            _player?.Stop();
            _player?.Dispose();

            _isSongActive = false;
        }

        string song = _queue.Dequeue();
        using Media media = new(_vlc, song, FromType.FromPath);
        _player = new MediaPlayer(media)
        {
            Volume = 10 * _volume
        };

        _player.EndReached += (_, _) =>
        {
            ThreadPool.QueueUserWorkItem(_ => DequeuePlay());
        };

        _player.Play();
        Console.WriteLine($"Playing {song}");
        _isSongActive = true;
    }

    public static List<string> GetSongFiles(string baseDirectory)
    {
        return [.. Directory.GetFiles(baseDirectory, "*.mp3", new EnumerationOptions()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true
        })];
    }

    public void PlayList(IEnumerable<string> songs)
    {
        _queue = new(songs);
        DequeuePlay();
    }

    public void Skip()
    {
        DequeuePlay();
    }

    public void Replay()
    {
        _player?.Position = 0;
    }

    public void Volume(int volume)
    {
        if (volume >= 10) volume = 10;
        _player?.Volume = 10 * volume;
        _volume = volume;
    }

    public void VolumeUpDown(bool increaseVolume)
    {
        if (increaseVolume) _volume++;
        else _volume--;

        if (_volume > 10) _volume = 10;
        if (_volume < 0) _volume = 0;

        _player?.Volume = _volume * 10;
    }

    public void PauseResume() => _player?.Pause();
}
