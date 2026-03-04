using Content.Shared._NF.Atmos.Components;
using Content.Shared.Atmos.Components;
using Robust.Shared.Spawners;

namespace Content.Server._Lua.Worldgen;

public sealed class SafeMiningSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<GasDepositExtractorComponent, ComponentStartup>(OnDrillStartup);
        SubscribeLocalEvent<GasDepositExtractorComponent, ComponentShutdown>(OnDrillShutdown);
        SubscribeLocalEvent<GasMinerComponent, ComponentStartup>(OnGasMinerStartup);
        SubscribeLocalEvent<GasMinerComponent, ComponentShutdown>(OnGasMinerShutdown);
    }

    private void OnDrillStartup(EntityUid uid, GasDepositExtractorComponent comp, ComponentStartup args)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid is not { Valid: true } grid) return;
        var freeze = EnsureComp<SafeMiningComponent>(grid);
        freeze.RefCount++;
        if (HasComp<TimedDespawnComponent>(grid)) RemCompDeferred<TimedDespawnComponent>(grid);
    }

    private void OnDrillShutdown(EntityUid uid, GasDepositExtractorComponent comp, ComponentShutdown args)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid is not { Valid: true } grid) return;
        if (TryComp<SafeMiningComponent>(grid, out var freeze) && freeze.RefCount > 0) { freeze.RefCount--; }
    }

    private void OnGasMinerStartup(EntityUid uid, GasMinerComponent comp, ComponentStartup args)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid is not { Valid: true } grid) return;
        var freeze = EnsureComp<SafeMiningComponent>(grid);
        freeze.RefCount++;
        if (HasComp<TimedDespawnComponent>(grid)) RemCompDeferred<TimedDespawnComponent>(grid);
    }

    private void OnGasMinerShutdown(EntityUid uid, GasMinerComponent comp, ComponentShutdown args)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid is not { Valid: true } grid) return;
        if (TryComp<SafeMiningComponent>(grid, out var freeze) && freeze.RefCount > 0) { freeze.RefCount--; }
    }
}


