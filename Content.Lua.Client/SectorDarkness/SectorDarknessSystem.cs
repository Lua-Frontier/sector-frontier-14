using Content.Client.Audio;
using Content.Client.Parallax;
using Content.Lua.Client.AmbientSpaceEffects;
using Content.Lua.Shared.SectorDarkness;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Lua.Client.SectorDarkness;

public sealed class SectorDarknessSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly ContentAudioSystem _contentAudio = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private const float FadeTime = 8f;
    private const float MusicFadeVolume = 32f;

    private SectorDarknessOverlay _overlay = default!;
    private SectorDarknessBackdropOverlay _backdrop = default!;
    private int _sources;
    private float _strength;
    private bool _dark;
    private EntityUid? _music;
    private float _musicVolume;
    private float _sliderVolume;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new SectorDarknessOverlay(_prototype);
        _backdrop = new SectorDarknessBackdropOverlay();
        SubscribeLocalEvent<SectorDarknessComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SectorDarknessComponent, ComponentShutdown>(OnShutdown);
        Subs.CVar(_cfg, CCVars.AmbientMusicVolume, OnMusicVolumeChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _strength = 0f;
        SetDark(false, null);
        Apply();
    }

    private void OnStartup(Entity<SectorDarknessComponent> ent, ref ComponentStartup args)
    {
        _sources++;
    }

    private void OnShutdown(Entity<SectorDarknessComponent> ent, ref ComponentShutdown args)
    {
        _sources--;
    }

    private void OnMusicVolumeChanged(float value)
    {
        _sliderVolume = SharedAudioSystem.GainToVolume(value);
        if (_music != null)
            _audio.SetVolume(_music, GetMusicVolume());
    }

    public override void FrameUpdate(float frameTime)
    {
        if (_sources == 0 && _strength == 0f && !_dark)
            return;

        var mapId = MapId.Nullspace;
        SectorDarknessComponent? darkness = null;
        if (_player.LocalEntity is { } player && TryComp(player, out TransformComponent? xform))
        {
            mapId = xform.MapID;
            TryComp(xform.MapUid, out darkness);
        }

        var dark = darkness != null;
        if (dark != _dark)
            SetDark(dark, darkness?.Music);

        if (dark)
        {
            _overlay.Map = mapId;
            _backdrop.Map = mapId;
        }

        var target = dark ? 1f : 0f;
        if (_strength == target)
            return;

        if (!dark && mapId != _overlay.Map)
            _strength = 0f;
        else if (dark)
            _strength = MathF.Min(1f, _strength + frameTime / FadeTime);
        else
            _strength = MathF.Max(0f, _strength - frameTime / FadeTime);

        Apply();
    }

    private void SetDark(bool dark, SoundSpecifier? music)
    {
        _dark = dark;
        _contentAudio.SetMusicSuppressed(nameof(SectorDarknessSystem), dark);

        if (!dark)
        {
            if (_strength <= 0f)
                _music = _audio.Stop(_music);

            return;
        }

        if (_music != null || music == null)
            return;

        _musicVolume = music.Params.Volume;
        _music = _audio.PlayGlobal(music, Filter.Local(), false, music.Params.WithLoop(true).WithVolume(GetMusicVolume()))?.Entity;
    }

    private float GetMusicVolume()
    {
        return _musicVolume + _sliderVolume - MusicFadeVolume * (1f - _strength);
    }

    private void Apply()
    {
        _overlay.Strength = _strength;
        _backdrop.Strength = _strength;

        if (_music != null)
        {
            if (_strength <= 0f && !_dark)
                _music = _audio.Stop(_music);
            else
                _audio.SetVolume(_music, GetMusicVolume());
        }

        if (_strength <= 0f)
        {
            SetNebulaZIndex(ParallaxSystem.ParallaxZIndex);
            _overlays.RemoveOverlay(_overlay);
            _overlays.RemoveOverlay(_backdrop);
        }
        else if (!_overlays.HasOverlay<SectorDarknessOverlay>())
        {
            SetNebulaZIndex(SectorDarknessBackdropOverlay.BackdropZIndex + 1);
            _overlays.AddOverlay(_overlay);
            _overlays.AddOverlay(_backdrop);
        }
    }

    private void SetNebulaZIndex(int zIndex)
    {
        if (_overlays.TryGetOverlay<AmbientSpaceEffectOverlay>(out var nebula))
            nebula.ZIndex = zIndex;
    }
}
