using Content.Server._Lua.SpaceWhale;
using Content.Server.Popups;
using Content.Shared.Lua.CLVar;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Goobstation.SpaceWhale;

public sealed class SpaceWhaleProximitySystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private bool _enabled;
    private float _safeZoneRadius;
    private const float DangerAmbientRadius = 25000f;
    private const string AmbientLoopPath = "/Audio/_Goobstation/Ambience/SpaceWhale/leviathan-ambient.ogg";
    private const string AmbientPulsePath = "/Audio/_Goobstation/Ambience/SpaceWhale/leviathan-appear.ogg";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2);
    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CLVars.SpaceWhaleEnabled, v => _enabled = v, true);
        Subs.CVar(_cfg, CLVars.SpaceWhaleSafeZoneRadius, v => _safeZoneRadius = v, true);
        SubscribeLocalEvent<SpaceWhaleProximityComponent, ComponentShutdown>(OnProximityShutdown);
        _nextCheck = _timing.CurTime + CheckInterval;
    }

    private void OnProximityShutdown(EntityUid uid, SpaceWhaleProximityComponent component, ComponentShutdown args)
    {
        component.AmbientStream = _audio.Stop(component.AmbientStream);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled)
            return;

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + CheckInterval;

        var safe2 = _safeZoneRadius * _safeZoneRadius;
        var danger2 = DangerAmbientRadius * DangerAmbientRadius;
        var query = EntityQueryEnumerator<MindContainerComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mind, out var mobState, out var xform))
        {
            var prox = EnsureComp<SpaceWhaleProximityComponent>(uid);

            if (!mind.HasMind)
            {
                prox.AmbientStream = _audio.Stop(prox.AmbientStream);
                continue;
            }

            if (mobState.CurrentState != MobState.Alive)
            {
                prox.AmbientStream = _audio.Stop(prox.AmbientStream);
                continue;
            }

            if (!HasComp<ActorComponent>(uid))
            {
                prox.AmbientStream = _audio.Stop(prox.AmbientStream);
                continue;
            }

            var pos = _transform.GetWorldPosition(xform);
            var isOutsideSafe = pos.LengthSquared() > safe2;
            var isOutsideDanger = pos.LengthSquared() > danger2;

            if (_player.TryGetSessionByEntity(uid, out var session))
            {
                if (isOutsideDanger)
                {
                    if (prox.AmbientStream == null)
                    {
                        var loopParams = AudioParams.Default.WithLoop(true).WithVolume(-8f);
                        prox.AmbientStream = _audio.PlayGlobal(new SoundPathSpecifier(AmbientLoopPath), session, loopParams)?.Entity;
                    }
                    if (_timing.CurTime >= prox.NextAppearCue)
                    {
                        _audio.PlayGlobal(new SoundPathSpecifier(AmbientPulsePath), session, AudioParams.Default.WithVolume(-15f));
                        prox.NextAppearCue = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(14f, 22f));
                    }
                }
                else
                {
                    prox.AmbientStream = _audio.Stop(prox.AmbientStream);
                    prox.NextAppearCue = TimeSpan.Zero;
                }
            }

            if (isOutsideSafe && !prox.WasOutsideSafeZone)
            {
                prox.WasOutsideSafeZone = true;
                _popup.PopupEntity(Loc.GetString("space-whale-warning"), uid, uid, PopupType.LargeCaution);
            }
            else if (!isOutsideSafe && prox.WasOutsideSafeZone)
            {
                prox.WasOutsideSafeZone = false;
                _popup.PopupEntity(Loc.GetString("space-whale-safe"), uid, uid, PopupType.Medium);
            }
        }
    }
}

