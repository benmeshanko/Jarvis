using PortAudioSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Whisper.net;

namespace Jarvis.Services;

public class VoiceListener : IDisposable
{
    const int Rate = 16000;
    readonly Channel<float[]> _audio = Channel.CreateUnbounded<float[]>();
    readonly WhisperProcessor _processor;
    readonly PortAudioSharp.Stream _stream;
    readonly PortAudioSharp.Stream.Callback _callback;

    readonly WhisperVadProcessor _vad;
    readonly List<float> _recent = [];
    static readonly Regex Wake = new(@"\b(?:jarvis|jervis|travis)\b[\s,.!?]*(.*)", RegexOptions.IgnoreCase);

    public VoiceListener(WhisperFactory factory, WhisperVadFactory vadFactory, string prompt)
    {
        _processor = factory.CreateBuilder()
            .WithLanguage("en")
            .WithPrompt(prompt)
            .Build();
        _vad = vadFactory.CreateBuilder().Build();

        _callback = OnAudio;

        PortAudio.Initialize();
        int dev = PortAudio.DefaultInputDevice;
        var p = new StreamParameters
        {
            device = dev,
            channelCount = 1,
            sampleFormat = SampleFormat.Float32,
            suggestedLatency = PortAudio.GetDeviceInfo(dev).defaultLowInputLatency,
            hostApiSpecificStreamInfo = IntPtr.Zero
        };
        _stream = new PortAudioSharp.Stream(p, null, Rate, 0, StreamFlags.ClipOff, _callback, IntPtr.Zero);
    }

    StreamCallbackResult OnAudio(IntPtr input, IntPtr output, uint frames,
        ref StreamCallbackTimeInfo time, StreamCallbackFlags flags, IntPtr user)
    {
        var buf = new float[frames];
        Marshal.Copy(input, buf, 0, (int)frames);
        _audio.Writer.TryWrite(buf);
        return StreamCallbackResult.Continue;
    }

    bool IsSpeech(float[] window) => _vad.DetectSpeech(window).Count > 0;

    public async Task RunAsync(Action<string> onCommand, CancellationToken ct)
    {
        const int Frame = Rate / 10;        // 100 ms of audio
        const int SilenceToEnd = 10;        // 1s of quiet ends an utterance
        const int MinSpeech = 1;            // ignore blips under 100 ms
        const int MaxFrames = 150;          // force processing at 15 s
        const int PreRoll = 3;              // keep 300 ms before speech starts

        _stream.Start();
        var pending = new List<float>();
        var preRoll = new Queue<float[]>();
        var utterance = new List<float[]>();
        int loudFrames = 0, quietFrames = 0;

        await foreach (var buf in _audio.Reader.ReadAllAsync(ct))
        {
            pending.AddRange(buf);
            while (pending.Count >= Frame)
            {
                var frame = pending.GetRange(0, Frame).ToArray();
                pending.RemoveRange(0, Frame);
                _recent.AddRange(frame);
                if (_recent.Count > Rate / 2) _recent.RemoveRange(0, _recent.Count - Rate / 2);
                bool loud = IsSpeech(_recent.ToArray());

                // Waiting for speech: keep a short rolling buffer so the first word isn't clipped
                if (utterance.Count == 0)
                {
                    if (!loud)
                    {
                        preRoll.Enqueue(frame);
                        if (preRoll.Count > PreRoll) preRoll.Dequeue();
                        continue;
                    }
                    utterance.AddRange(preRoll);
                    preRoll.Clear();
                }

                utterance.Add(frame);
                if (loud) { loudFrames++; quietFrames = 0; } else quietFrames++;

                if (quietFrames >= SilenceToEnd || utterance.Count >= MaxFrames)
                {
                    if (loudFrames >= MinSpeech)
                    {
                        var samples = utterance.SelectMany(f => f).ToList();
                        if (samples.Count < Rate)                  // pad to 1 s minimum
                            samples.AddRange(new float[Rate - samples.Count]);

                        var text = new StringBuilder();

                        Stopwatch sw = Stopwatch.StartNew();

                        await foreach (var seg in _processor.ProcessAsync(samples.ToArray(), ct))
                            text.Append(seg.Text);

                        sw.Stop();

                        Console.WriteLine($"[Heard] {text}. Processed in {sw.ElapsedMilliseconds}ms");

                        var m = Wake.Match(text.ToString());
                        if (m.Success) onCommand(m.Groups[1].Value.Trim());
                    }
                    utterance.Clear();
                    loudFrames = quietFrames = 0;
                }
            }
        }
    }

    public void Dispose()
    {
        _stream.Stop();
        _stream.Dispose();
        _processor.Dispose();
        PortAudio.Terminate();
    }
}
