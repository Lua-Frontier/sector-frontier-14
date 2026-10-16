using Content.Client.Audio;
using Content.Client.Parallax;
using Content.Lua.Client.AmbientSpaceEffects;
using Content.Lua.Shared.SectorBrightness;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Lua.Client.SectorBrightness;

public sealed class SectorBrightnessSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly ContentAudioSystem _contentAudio = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private const float FadeTime = 8f;
    private const float MusicFadeVolume = 32f;

    private SectorBrightnessBackdropOverlay _backdrop = default!;
    private int _sources;
    private float _strength;
    private bool _bright;
    private EntityUid? _music;
    private float _musicVolume;
    private float _sliderVolume;
    private EntityUid? _lightMap;
    private Color _baseColor;
    private Color _ambientColor;

    public override void Initialize()
    {
        base.Initialize();

        _backdrop = new SectorBrightnessBackdropOverlay();
        SubscribeLocalEvent<SectorBrightnessComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SectorBrightnessComponent, ComponentShutdown>(OnShutdown);
        Subs.CVar(_cfg, CCVars.AmbientMusicVolume, OnMusicVolumeChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _strength = 0f;
        SetBright(false, null);
        Apply();
    }

    private void OnStartup(Entity<SectorBrightnessComponent> ent, ref ComponentStartup args)
    {
        _sources++;
    }

    private void OnShutdown(Entity<SectorBrightnessComponent> ent, ref ComponentShutdown args)
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
        if (_sources == 0 && _strength == 0f && !_bright)
            return;

        var mapId = MapId.Nullspace;
        EntityUid? mapUid = null;
        SectorBrightnessComponent? brightness = null;
        if (_player.LocalEntity is { } player && TryComp(player, out TransformComponent? xform))
        {
            mapId = xform.MapID;
            mapUid = xform.MapUid;
            TryComp(mapUid, out brightness);
        }

        var bright = brightness != null;
        if (bright != _bright)
            SetBright(bright, brightness?.Music);

        if (brightness != null)
        {
            _backdrop.Map = mapId;
            _ambientColor = brightness.AmbientColor;
            if (_lightMap != mapUid)
            {
                RestoreLight();
                _lightMap = mapUid;
                _baseColor = CompOrNull<MapLightComponent>(mapUid)?.AmbientLightColor ?? MapLightComponent.DefaultColor;
            }
        }

        var target = bright ? 1f : 0f;
        if (_strength == target)
            return;

        if (!bright && mapId != _backdrop.Map)
            _strength = 0f;
        else if (bright)
            _strength = MathF.Min(1f, _strength + frameTime / FadeTime);
        else
            _strength = MathF.Max(0f, _strength - frameTime / FadeTime);

        Apply();
    }

    private void SetBright(bool bright, SoundSpecifier? music)
    {
        _bright = bright;
        _contentAudio.SetMusicSuppressed(nameof(SectorBrightnessSystem), bright);

        if (!bright)
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
        _backdrop.Strength = _strength;

        if (_strength <= 0f)
            RestoreLight();
        else if (TryComp(_lightMap, out MapLightComponent? light))
            light.AmbientLightColor = Color.InterpolateBetween(_baseColor, _ambientColor, _strength);

        if (_music != null)
        {
            if (_strength <= 0f && !_bright)
                _music = _audio.Stop(_music);
            else
                _audio.SetVolume(_music, GetMusicVolume());
        }

        if (_strength <= 0f)
        {
            SetNebulaZIndex(ParallaxSystem.ParallaxZIndex);
            _overlays.RemoveOverlay(_backdrop);
        }
        else if (!_overlays.HasOverlay<SectorBrightnessBackdropOverlay>())
        {
            SetNebulaZIndex(SectorBrightnessBackdropOverlay.BackdropZIndex + 1);
            _overlays.AddOverlay(_backdrop);
        }
    }

    private void RestoreLight()
    {
        if (TryComp(_lightMap, out MapLightComponent? light))
            light.AmbientLightColor = _baseColor;

        _lightMap = null;
    }

    private void SetNebulaZIndex(int zIndex)
    {
        if (_overlays.TryGetOverlay<AmbientSpaceEffectOverlay>(out var nebula))
            nebula.ZIndex = zIndex;
    }
}
