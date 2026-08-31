using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server._Mono.FireControl;
using Content.Shared._Mono;
using Content.Shared._Mono.SpaceArtillery;
using Content.Shared._NF.Shuttles.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Timing;
using Content.Server._Lua.Shuttles.Systems;

namespace Content.Server.Shuttles.Systems;

public sealed class ShuttleFTLSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly ShuttleGridAccessSystem _gridAccess = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<ShuttleFTLComponent>();

        while (query.MoveNext(out var uid, out var shuttleFtl))
        {
            if (!shuttleFtl.InCombat || shuttleFtl.CombatUntil > curTime)
                continue;

            shuttleFtl.InCombat = false;
            Dirty(uid, shuttleFtl);
            _console.RefreshShuttleConsoles(uid);
        }
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FireControllableComponent, AmmoShotEvent>(OnWeaponShot);
        // ProjectileGridPhaseComponent: unique pair; ShipWeaponProjectileComponent is already used by SpaceArtillerySystem.
        SubscribeLocalEvent<ProjectileGridPhaseComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<ConsoleFTLAttemptEvent>(OnConsoleFTLAttempt);
    }

    private void OnWeaponShot(EntityUid uid, FireControllableComponent component, ref AmmoShotEvent args)
    {
        var gridUid = Transform(uid).GridUid;
        if (gridUid == null || !_gridAccess.IsMobileShuttle(gridUid.Value))
            return;

        MarkShuttleGroupInCombat(gridUid.Value);

        foreach (var projectileUid in args.FiredProjectiles)
        {
            if (!HasComp<ShipWeaponProjectileComponent>(projectileUid))
                continue;

            var phase = EnsureComp<ProjectileGridPhaseComponent>(projectileUid);
            phase.SourceGrid = gridUid;
        }
    }

    private void OnProjectileHit(EntityUid uid, ProjectileGridPhaseComponent phase, ref ProjectileHitEvent args)
    {
        var targetGridUid = Transform(args.Target).GridUid;
        if (targetGridUid == null || !_gridAccess.IsMobileShuttle(targetGridUid.Value))
            return;

        if (phase.SourceGrid == null || phase.SourceGrid == targetGridUid)
            return;

        MarkShuttleGroupInCombat(targetGridUid.Value);
    }

    private void OnConsoleFTLAttempt(ref ConsoleFTLAttemptEvent ev)
    {
        if (!TryComp<ShuttleFTLComponent>(ev.Uid, out var ftl) || ftl.CombatUntil <= _timing.CurTime)
            return;

        ev.Cancelled = true;
        ev.Reason = Loc.GetString("shuttle-console-in-combat");
    }

    private void MarkShuttleGroupInCombat(EntityUid shuttleUid)
    {
        var dockedShuttles = new HashSet<EntityUid>();
        _shuttle.GetAllDockedShuttlesIgnoringFTLLock(shuttleUid, dockedShuttles);

        foreach (var dockedUid in dockedShuttles)
        {
            if (!_gridAccess.IsMobileShuttle(dockedUid))
                continue;

            var ftl = EnsureComp<ShuttleFTLComponent>(dockedUid);
            var wasInCombat = ftl.InCombat;

            ftl.CombatUntil = _timing.CurTime + ftl.CombatCooldown;

            if (!wasInCombat)
            {
                ftl.InCombat = true;
                Dirty(dockedUid, ftl);
                _console.RefreshShuttleConsoles(dockedUid);
            }
        }
    }
}
