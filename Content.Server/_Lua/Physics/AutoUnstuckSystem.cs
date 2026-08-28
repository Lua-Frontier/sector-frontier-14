// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server.Shuttles.Components;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using System.Numerics;

namespace Content.Server._Lua.Physics;

[UsedImplicitly]
public sealed class AutoUnstuckSystem : EntitySystem
{
    private static readonly Vector2[][] StuckOffsetRings =
    [
        [
            new(1f, 0f),
            new(-1f, 0f),
            new(0f, 1f),
            new(0f, -1f),
        ],
        [
            new(1f, 1f),
            new(1f, -1f),
            new(-1f, 1f),
            new(-1f, -1f),
            new(2f, 0f),
            new(-2f, 0f),
            new(0f, 2f),
            new(0f, -2f),
        ],
        [
            new(2f, 1f),
            new(2f, -1f),
            new(-2f, 1f),
            new(-2f, -1f),
            new(1f, 2f),
            new(1f, -2f),
            new(-1f, 2f),
            new(-1f, -2f),
            new(3f, 0f),
            new(-3f, 0f),
            new(0f, 3f),
            new(0f, -3f),
        ],
    ];

    private const float ScanIntervalSeconds = 3f;
    private const float StuckSeconds = 15f;
    private const float MaxCandidateSpeedSquared = 0.01f;

    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly Dictionary<EntityUid, float> _stuckTime = new();
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<MobStateComponent> _mobQuery;
    private readonly List<EntityUid> _toClear = new();
    private readonly List<EntityUid> _awake = new();
    private readonly List<Vector2> _offsetScratch = new(16);
    private float _scanAccum;

    public override void Initialize()
    {
        base.Initialize();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _mobQuery = GetEntityQuery<MobStateComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_stuckTime.Count > 0)
        {
            _toClear.Clear();
            foreach (var (uid, _) in _stuckTime)
            {
                if (!Exists(uid))
                    _toClear.Add(uid);
            }

            foreach (var uid in _toClear)
            {
                _stuckTime.Remove(uid);
            }
        }

        _scanAccum += frameTime;
        if (_scanAccum < ScanIntervalSeconds)
            return;

        var dt = _scanAccum;
        _scanAccum = 0f;
        ScanBodies(dt);
    }

    private void ScanBodies(float frameTime)
    {
        _toClear.Clear();
        _awake.Clear();

        foreach (var ent in _physics.AwakeBodies)
        {
            _awake.Add(ent.Owner);
        }

        foreach (var uid in _awake)
        {
            if (!_mobQuery.HasComponent(uid))
                continue;

            if (!_physicsQuery.TryGetComponent(uid, out var body))
                continue;

            if (body.BodyType == BodyType.Static || !body.CanCollide)
                continue;

            if (HasComp<MapGridComponent>(uid) || HasComp<MapComponent>(uid) || HasComp<ShuttleComponent>(uid))
                continue;

            if (IsPaused(uid))
                continue;

            if (!_xformQuery.TryGetComponent(uid, out var xform) || xform.Anchored)
            {
                _toClear.Add(uid);
                continue;
            }

            if (!_stuckTime.ContainsKey(uid) && body.LinearVelocity.LengthSquared() > MaxCandidateSpeedSquared)
                continue;

            if (!IsEmbeddedInHardStatic(uid, xform))
            {
                _toClear.Add(uid);
                continue;
            }

            if (_stuckTime.TryGetValue(uid, out var t))
                _stuckTime[uid] = t + frameTime;
            else
                _stuckTime[uid] = frameTime;

            if (_stuckTime[uid] < StuckSeconds)
                continue;

            TryEject(uid, body, xform);
            _toClear.Add(uid);
        }

        foreach (var uid in _toClear)
        {
            _stuckTime.Remove(uid);
        }
    }

    private bool IsEmbeddedInHardStatic(EntityUid uid, TransformComponent xform)
    {
        if (!_turf.TryGetTileRef(xform.Coordinates, out var tile) || tile.Value.Tile.IsEmpty)
            return false;

        if (!_turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable))
            return false;

        var contacts = _physics.GetContacts(uid);
        while (contacts.MoveNext(out var contact))
        {
            if (!contact.IsTouching || !contact.Hard)
                continue;

            var otherBody = contact.OtherBody(uid);
            if (otherBody.BodyType == BodyType.Static)
                return true;
        }

        return false;
    }

    private void TryEject(EntityUid uid, PhysicsComponent body, TransformComponent xform)
    {
        foreach (var ring in StuckOffsetRings)
        {
            _offsetScratch.Clear();
            _offsetScratch.AddRange(ring);
            _random.Shuffle(_offsetScratch);

            foreach (var offset in _offsetScratch)
            {
                var target = xform.Coordinates.Offset(offset);
                if (!IsSafeLanding(target))
                    continue;

                _xform.SetCoordinates(uid, xform, target);
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
                _physics.WakeBody(uid, body: body);
                return;
            }
        }
    }

    private bool IsSafeLanding(EntityCoordinates coords)
    {
        if (!_turf.TryGetTileRef(coords, out var tile) || tile.Value.Tile.IsEmpty)
            return false;

        if (_turf.IsSpace(tile.Value))
            return false;

        if (_turf.IsTileBlocked(tile.Value, CollisionGroup.MobMask))
            return false;

        return true;
    }
}
