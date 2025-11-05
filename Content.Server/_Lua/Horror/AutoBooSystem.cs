using Content.Server.Ghost;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lua.Horror;

public sealed class AutoBooSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AutoBooComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, AutoBooComponent comp, MapInitEvent args)
    {
        if (comp.SpawnAnomalyOnInit == null || comp.AnomalySpawned) return;
        Timer.Spawn(TimeSpan.FromMilliseconds(200), () =>
        {
            if (Deleted(uid) || comp.AnomalySpawned) return;
            var xform = Transform(uid);
            Spawn(comp.SpawnAnomalyOnInit, xform.Coordinates);
            comp.AnomalySpawned = true;
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AutoBooComponent, TransformComponent, MobStateComponent>();

        while (query.MoveNext(out var uid, out var comp, out var xform, out var mobState))
        {
            if (!_mobState.IsAlive(uid, mobState)) continue;
            var shouldBoo = now >= comp.NextBoo;
            var shouldPlaySound = now >= comp.NextSound;
            if (!shouldBoo && !shouldPlaySound) continue;

            if (shouldBoo)
            {
                comp.NextBoo = now + comp.BooInterval;
                var entities = _lookup.GetEntitiesInRange(xform.Coordinates, comp.BooRadius);
                var targets = new List<EntityUid>(entities);
                _random.Shuffle(targets);
                var booCounter = 0;
                foreach (var target in targets)
                {
                    if (target == uid) continue;
                    var handled = _ghost.DoGhostBooEvent(target);
                    if (handled) booCounter++;
                    if (booCounter >= comp.BooMaxTargets) break;
                }
            }
            if (shouldPlaySound)
            { comp.NextSound = now + comp.SoundInterval; _audio.PlayPvs(comp.BooSound, uid); }
        }
    }
}

