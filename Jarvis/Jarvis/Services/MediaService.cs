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
    private readonly StatisticsService _statisticsService;
    private readonly ILogger<MediaService> _logger;

    public MediaService(StatisticsService statisticsService, ILogger<MediaService> logger)
    {
        _statisticsService = statisticsService;
        _logger = logger;

        Core.Initialize();
        _vlc = new();
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
        _logger.LogInformation($"Playing {song}");
        _ = Task.Run(() => _statisticsService.RecordSongAsync(song));
        _isSongActive = true;
    }

    public void PlayList(IEnumerable<string> songs)
    {
        _queue = new(songs);
        DequeuePlay();
    }

    public void Skip() => DequeuePlay();
    public void Replay() => _player?.Position = 0;
    public void Pause() => _player?.Pause();

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
}
