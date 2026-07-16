using System.Text.Json;
using System.Windows.Media;

namespace BlobForge.Audio;

public enum SoundCue
{
    FactoryHum,
    Breaker,
    BlobDrop,
    Chamber,
    Conveyor,
    Crusher,
    Drill,
    Press,
    Vacuum,
    Filter,
    Cart,
    Music
}

public enum AudioBus
{
    Sfx,
    Music
}

public sealed class SoundEffectSetting
{
    public string Path { get; set; } = string.Empty;
    public int Volume { get; set; } = 80;
    public AudioBus Bus { get; set; } = AudioBus.Sfx;
}

public sealed class SoundEffectSettings
{
    public int MasterVolume { get; set; } = 80;
    public int SfxVolume { get; set; } = 80;
    public int MusicVolume { get; set; } = 80;
    public Dictionary<string, SoundEffectSetting> Effects { get; set; } = new();
}

/// <summary>
/// Each game component owns an asynchronous media player, so custom WAV/MP3 files
/// can overlap, loop, and be levelled independently without blocking the game thread.
/// </summary>
public sealed class SoundEffectMixer : IDisposable
{
    private readonly string _settingsPath;
    private readonly Dictionary<SoundCue, MediaPlayer> _players = new();
    private readonly HashSet<SoundCue> _requestedLoopChannels = new();
    private SoundEffectSettings _settings;

    public SoundEffectMixer(string? settingsRoot = null)
    {
        var root = settingsRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BlobForge");
        Directory.CreateDirectory(root);
        _settingsPath = Path.Combine(root, "audio-settings.json");
        _settings = LoadSettings(_settingsPath);
        EnsureSettings(root);
        Save();
        foreach (var cue in Enum.GetValues<SoundCue>())
        {
            var player = new MediaPlayer();
            player.MediaEnded += (_, _) => RestartLoop(cue);
            _players[cue] = player;
            OpenPlayer(cue);
        }
    }

    public int MasterVolume
    {
        get => _settings.MasterVolume;
        set
        {
            _settings.MasterVolume = Math.Clamp(value, 0, 100);
            RefreshOpenChannelVolumes();
            Save();
        }
    }

    public int SfxVolume
    {
        get => _settings.SfxVolume;
        set
        {
            _settings.SfxVolume = Math.Clamp(value, 0, 100);
            RefreshOpenChannelVolumes();
            Save();
        }
    }

    public int MusicVolume
    {
        get => _settings.MusicVolume;
        set
        {
            _settings.MusicVolume = Math.Clamp(value, 0, 100);
            RefreshOpenChannelVolumes();
            Save();
        }
    }

    public SoundEffectSetting Get(SoundCue cue) => _settings.Effects[cue.ToString()];

    public float EffectiveVolume(SoundCue cue)
    {
        var setting = Get(cue);
        var busVolume = setting.Bus == AudioBus.Music ? MusicVolume : SfxVolume;
        return Math.Clamp(
            MasterVolume / 100f * busVolume / 100f * setting.Volume / 100f,
            0f,
            1f);
    }

    public void SetPath(SoundCue cue, string path)
    {
        _requestedLoopChannels.Remove(cue);
        _players[cue].Stop();
        _players[cue].Close();
        Get(cue).Path = path;
        OpenPlayer(cue);
        Save();
    }

    public void SetVolume(SoundCue cue, int volume)
    {
        Get(cue).Volume = Math.Clamp(volume, 0, 100);
        ApplyVolume(cue);
        Save();
    }

    public void SetBus(SoundCue cue, AudioBus bus)
    {
        Get(cue).Bus = bus;
        ApplyVolume(cue);
        Save();
    }

    public void Play(SoundCue cue)
    {
        _requestedLoopChannels.Remove(cue);
        var player = _players[cue];
        player.Stop();
        player.Position = TimeSpan.Zero;
        player.Play();
    }

    public void SetLooping(SoundCue cue, bool shouldLoop)
    {
        if (shouldLoop)
        {
            // Desired state is the idempotence guard. The fixed update reports the
            // same state at 120 Hz; playback work must happen only on transitions.
            if (!_requestedLoopChannels.Add(cue)) return;
            var player = _players[cue];
            player.Stop();
            player.Position = TimeSpan.Zero;
            player.Play();
            return;
        }

        if (!_requestedLoopChannels.Remove(cue)) return;
        _players[cue].Stop();
    }

    public void StopAll()
    {
        _requestedLoopChannels.Clear();
        foreach (var player in _players.Values) player.Stop();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Audio customization should never prevent the game from running.
        }
    }

    public void Dispose()
    {
        StopAll();
        foreach (var player in _players.Values) player.Close();
        _players.Clear();
    }

    private void RefreshOpenChannelVolumes()
    {
        foreach (var cue in _players.Keys) ApplyVolume(cue);
    }

    private void ApplyVolume(SoundCue cue)
    {
        if (!_players.TryGetValue(cue, out var player)) return;
        player.Volume = EffectiveVolume(cue);
    }

    private void OpenPlayer(SoundCue cue)
    {
        var path = Get(cue).Path;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try
        {
            _players[cue].Open(new System.Uri(Path.GetFullPath(path), System.UriKind.Absolute));
            ApplyVolume(cue);
        }
        catch
        {
            // Invalid custom audio is isolated to its cue and never blocks gameplay.
        }
    }

    private void RestartLoop(SoundCue cue)
    {
        if (!_requestedLoopChannels.Contains(cue) || !_players.TryGetValue(cue, out var player)) return;
        player.Position = TimeSpan.Zero;
        player.Play();
    }

    private void EnsureSettings(string root)
    {
        var audioRoot = Path.Combine(root, "DefaultAudio");
        Directory.CreateDirectory(audioRoot);
        foreach (var cue in Enum.GetValues<SoundCue>())
        {
            if (!_settings.Effects.TryGetValue(cue.ToString(), out var setting))
            {
                setting = new SoundEffectSetting
                {
                    Volume = DefaultVolume(cue),
                    Bus = DefaultBus(cue)
                };
                _settings.Effects[cue.ToString()] = setting;
            }
            if (!string.IsNullOrWhiteSpace(setting.Path) && File.Exists(setting.Path)) continue;
            if (cue == SoundCue.Music)
            {
                // Reserved empty channel for music the project adds later. It is
                // already routed through the Music slider without inventing a
                // placeholder song or exposing asset selection in game settings.
                setting.Path = string.Empty;
                setting.Bus = AudioBus.Music;
                continue;
            }
            var path = Path.Combine(audioRoot, $"{cue}.wav");
            if (!File.Exists(path)) GenerateDefault(cue, path);
            setting.Path = path;
        }
    }

    private static SoundEffectSettings LoadSettings(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<SoundEffectSettings>(File.ReadAllText(path)) ?? new();
        }
        catch
        {
            // Fall back to a valid default document.
        }
        return new SoundEffectSettings();
    }

    private static int DefaultVolume(SoundCue cue) => cue switch
    {
        SoundCue.FactoryHum => 35,
        SoundCue.Conveyor => 28,
        SoundCue.Drill => 58,
        SoundCue.Vacuum => 48,
        SoundCue.Music => 100,
        _ => 72
    };

    private static AudioBus DefaultBus(SoundCue cue)
        => cue == SoundCue.Music ? AudioBus.Music : AudioBus.Sfx;

    private static void GenerateDefault(SoundCue cue, string path)
    {
        var duration = cue switch
        {
            SoundCue.FactoryHum => 2.0,
            SoundCue.Drill or SoundCue.Vacuum or SoundCue.Press => 0.8,
            SoundCue.Conveyor => 1.0,
            SoundCue.Crusher => 0.62,
            SoundCue.Filter or SoundCue.Cart => 0.48,
            SoundCue.BlobDrop => 0.38,
            _ => 0.25
        };
        var random = new Random(3107 + (int)cue * 97);
        WriteWave(path, duration, (t, progress) =>
        {
            var noise = random.NextDouble() * 2d - 1d;
            var fade = cue == SoundCue.FactoryHum ? 1d : Math.Pow(1d - progress, 1.4d);
            return cue switch
            {
                SoundCue.FactoryHum => 0.23 * Math.Sin(t * Math.PI * 2d * 55d) +
                                       0.08 * Math.Sin(t * Math.PI * 2d * 110d),
                SoundCue.Breaker => fade * (0.62 * noise + 0.28 * Math.Sin(t * Math.PI * 2d * 180d)),
                SoundCue.BlobDrop => fade * (0.34 * Math.Sin(t * Math.PI * 2d * (105d - progress * 48d)) + 0.12 * noise),
                SoundCue.Chamber => fade * (0.30 * Math.Sin(t * Math.PI * 2d * 64d) + 0.18 * noise),
                SoundCue.Conveyor => 0.18 * Math.Sin(t * Math.PI * 2d * 44d) +
                                     0.10 * Math.Sin(t * Math.PI * 2d * 88d) + 0.05 * noise,
                SoundCue.Crusher => fade * (0.42 * Math.Sin(t * Math.PI * 2d * 47d) + 0.23 * noise),
                SoundCue.Drill => 0.30 * Math.Sin(t * Math.PI * 2d * 92d) + 0.22 * Math.Sin(t * Math.PI * 2d * 184d) + 0.12 * noise,
                SoundCue.Press => 0.20 * Math.Sin(t * Math.PI * 2d * 52d) +
                                  0.13 * Math.Sin(t * Math.PI * 2d * 104d) + 0.08 * noise,
                SoundCue.Vacuum => 0.19 * Math.Sin(t * Math.PI * 2d * 68d) + 0.22 * noise,
                SoundCue.Filter => fade * (0.34 * Math.Sin(t * Math.PI * 2d * 310d) + 0.13 * noise),
                SoundCue.Cart => fade * (0.34 * Math.Sin(t * Math.PI * 2d * 76d) + 0.12 * noise),
                _ => 0d
            };
        });
    }

    private static void WriteWave(string path, double seconds, Func<double, double, double> sample)
    {
        const int sampleRate = 22050;
        const short channels = 1;
        const short bits = 16;
        var sampleCount = Math.Max(1, (int)Math.Round(seconds * sampleRate));
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + sampleCount * 2);
        writer.Write("WAVEfmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bits / 8);
        writer.Write((short)(channels * bits / 8));
        writer.Write(bits);
        writer.Write("data"u8.ToArray());
        writer.Write(sampleCount * 2);
        for (var i = 0; i < sampleCount; i++)
        {
            var value = Math.Clamp(sample(i / (double)sampleRate, i / (double)sampleCount), -0.92, 0.92);
            writer.Write((short)Math.Round(value * short.MaxValue));
        }
    }

}
