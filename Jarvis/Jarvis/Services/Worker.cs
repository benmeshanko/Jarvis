using System.Text.RegularExpressions;
using Whisper.net;
using Whisper.net.Ggml;

namespace Jarvis.Services;

/// <summary>
/// Background service, orchestrates program functions.
/// </summary>
public class Worker(
    ILogger<Worker> logger,
    MediaService mediaService,
    LibraryService libraryService) : BackgroundService
{
    private static async Task DownloadWhisperModel(string model, bool vad)
    {
        if (!File.Exists(model))
        {
            Stream? downloadStream = null;
            try
            {
                if (vad) downloadStream = await WhisperGgmlDownloader.Default.GetGgmlSileroVadModelAsync();
                else downloadStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType.TinyEn);
                using var modelFile = File.OpenWrite(model);
                await downloadStream.CopyToAsync(modelFile);
            }
            finally
            {
                downloadStream?.Dispose();
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string model = "ggml-tinyen.bin";
        await DownloadWhisperModel(model, false);
        using var factory = WhisperFactory.FromPath(model);

        string vadModel = "ggml-silero-v6.2.0.bin";
        await DownloadWhisperModel(vadModel, true);
        using var vadFactory = WhisperVadFactory.FromPath(vadModel);

        using var listener = new VoiceListener(logger, factory, vadFactory, "Jarvis.");
        using var cts = new CancellationTokenSource();
        var listening = listener.RunAsync(command => Handle(command, cts), cts.Token);

        try { await listening; } catch (OperationCanceledException) { }
    }

    static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["quit"] = "quit",
        ["skip"] = "skip",
        ["pause"] = "pause",
        ["paws"] = "pause",
        ["resume"] = "resume",
        ["re-zoom"] = "resume",
        ["for zoom"] = "resume",
        ["shuffle"] = "shuffle",
        ["play"] = "play",
        ["plays"] = "play",
        ["played"] = "play",
        ["volume"] = "volume",
        ["replay"] = "replay",
    };

    static readonly Dictionary<string, int> Numbers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zero"] = 0,
        ["one"] = 1,
        ["two"] = 2,
        ["three"] = 3,
        ["four"] = 4,
        ["five"] = 5,
        ["six"] = 6,
        ["seven"] = 7,
        ["eight"] = 8,
        ["nine"] = 9,
        ["ten"] = 10,
    };

    void Handle(string command, CancellationTokenSource cts)
    {
        logger.LogInformation($"[Command] {command}");
        command = Regex.Replace(command, @"[^\w\s']", " ").Trim();
        var words = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var action = words.Length > 0 ? Aliases.GetValueOrDefault(words[0]) : null;
        var target = words.Length > 1 ? words[1] : "";

        if (action is "quit")
            cts.Cancel();
        else if (action is "skip")
            mediaService.Skip();
        else if (action is "pause" or "resume")
            mediaService.PauseResume();
        else if (action is "shuffle" && libraryService.Find(target) is Target shuffleTarget)
            mediaService.PlayList(libraryService.Songs(shuffleTarget, true));
        else if (action is "play" && libraryService.Find(target) is Target playTarget)
            mediaService.PlayList(libraryService.Songs(playTarget, false));
        else if (action is "replay")
            mediaService.Replay();
        else if (action is "volume")
        {
            if (target == "up") mediaService.VolumeUpDown(true);
            else if (target == "down") mediaService.VolumeUpDown(false);
            else if (int.TryParse(target, out int volume)
                || Numbers.TryGetValue(target, out volume)) mediaService.Volume(volume);
        }
    }
}
