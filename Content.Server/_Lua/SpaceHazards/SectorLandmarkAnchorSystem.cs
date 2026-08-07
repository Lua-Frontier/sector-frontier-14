// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Server._Mono.Cleanup;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.SpaceHazards;
using Content.Shared.Singularity.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Lua.SpaceHazards;

public sealed class SectorLandmarkAnchorSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SectorCelestialBodyComponent, EntParentChangedMessage>(OnCelestialParent);
        SubscribeLocalEvent<SectorBackgroundPlanetComponent, EntParentChangedMessage>(OnPlanetParent);
        SubscribeLocalEvent<AmbientSpaceFieldComponent, EntParentChangedMessage>(OnFieldParent);
    }

    private void OnCelestialParent(EntityUid uid, SectorCelestialBodyComponent _, ref EntParentChangedMessage args)
        => LockToMap(uid);
    private void OnPlanetParent(EntityUid uid, SectorBackgroundPlanetComponent _, ref EntParentChangedMessage args)
        => LockToMap(uid);
    private void OnFieldParent(EntityUid uid, AmbientSpaceFieldComponent _, ref EntParentChangedMessage args)
        => LockToMap(uid);
    public void LockToMap(EntityUid uid)
    {
        if (HasComp<SectorCelestialBodyComponent>(uid) || HasComp<EventHorizonComponent>(uid))
            EnsureComp<CleanupImmuneComponent>(uid);

        if (TryComp<PhysicsComponent>(uid, out var body) && !HasComp<EventHorizonComponent>(uid))
        {
            _physics.SetCanCollide(uid, false, body: body);
            RemCompDeferred<FixturesComponent>(uid);
            RemCompDeferred<PhysicsComponent>(uid);
        }
        var xform = Transform(uid);
        if (xform.MapUid is not { } mapUid) return;
        if (xform.ParentUid == mapUid) return;
        if (HasComp<MapGridComponent>(xform.ParentUid) || xform.GridUid != null) _transform.SetParent(uid, xform, mapUid);
    }
}
