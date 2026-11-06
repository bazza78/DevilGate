using Godot;
namespace DevilGate.Scripts.Systems;

/// <summary>Quiet original procedural placeholders; one ambience player crossfades between areas.</summary>
public partial class GameAudio : Node
{
    public static GameAudio? Instance { get; private set; }
    private AudioStreamPlayer _effects = null!, _ambient = null!;
    private readonly Dictionary<string, AudioStreamWav> _clips = new();
    private string _area = "";
    private float _ambientGain;
    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Pausable;
        _effects = new AudioStreamPlayer { MaxPolyphony = 4 }; AddChild(_effects);
        _ambient = new AudioStreamPlayer(); AddChild(_ambient);
        foreach (var (key, hz) in new[] { ("step", 100f), ("chest", 180f), ("equip", 440f), ("potion", 740f), ("skill", 310f), ("relay", 880f) })
            _clips[key] = Tone(hz, .16f, false);
        _clips["home"] = Tone(65, 2, true); _clips["outside"] = Tone(95, 2, true);
    }
    private static AudioStreamWav Tone(float hz, float seconds, bool loop)
    {
        const int rate = 22050;
        int count = (int)(seconds * rate); var bytes = new byte[count * 2];
        for (int i = 0; i < count; i++)
        {
            double t = (double)i / rate;
            double envelope = loop ? .1 : Math.Sin(Math.PI * i / count) * Math.Exp(-5.0 * i / count);
            short sample = (short)((Math.Sin(Math.Tau * hz * t) + .25 * Math.Sin(Math.Tau * hz * 2 * t)) * envelope * 9000);
            bytes[i * 2] = (byte)(sample & 255); bytes[i * 2 + 1] = (byte)((sample >> 8) & 255);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate, Data = bytes, LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled, LoopEnd = count };
    }
    public void Cue(string key)
    {
        if (DisplayServer.GetName() == "headless" || !_clips.TryGetValue(key, out var clip)) return;
        _effects.VolumeLinear = ComfortSettings.Master * ComfortSettings.Effects * .15f;
        _effects.Stream = clip; _effects.Play();
    }
    public override void _Process(double delta)
    {
        if (DisplayServer.GetName() == "headless") return;
        var run = GameSession.Instance?.CurrentRun;
        string next = run == null ? "" : run.LocationId == "safehouse" ? "home" : "outside";
        _ambientGain = Mathf.MoveToward(_ambientGain, next == _area && next != "" ? 1 : 0, (float)delta * 2);
        _ambient.VolumeLinear = _ambientGain * ComfortSettings.Master * ComfortSettings.Ambience * .2f;
        if (next != _area && _ambientGain == 0)
        {
            _ambient.Stop(); _area = next;
            if (next != "") { _ambient.Stream = _clips[next]; _ambient.Play(); }
        }
    }
    public override void _ExitTree()
    {
        _effects.Stop(); _ambient.Stop(); _effects.Stream = null; _ambient.Stream = null;
        foreach (var clip in _clips.Values) clip.Dispose();
        _clips.Clear();
        if (Instance == this) Instance = null;
    }
}
