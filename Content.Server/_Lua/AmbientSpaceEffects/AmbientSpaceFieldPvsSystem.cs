// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Shared._Lua.AmbientSpaceEffects;
using Robust.Server.GameStates;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server._Lua.AmbientSpaceEffects;

public sealed class AmbientSpaceFieldPvsSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly PvsOverrideSystem _pvs = default!;

    private readonly Dictionary<ICommonSession, HashSet<EntityUid>> _sessionFields = new();
    private readonly HashSet<EntityUid> _desired = new();
    private readonly List<EntityUid> _removeScratch = new();

    public override void Initialize()
    {
        base.Initialize();
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<ActorComponent, EntParentChangedMessage>(OnActorParentChanged);
        SubscribeLocalEvent<MapGridComponent, EntParentChangedMessage>(OnGridParentChanged);
        SubscribeLocalEvent<AmbientSpaceFieldComponent, MapInitEvent>(OnFieldMapInit);
        SubscribeLocalEvent<AmbientSpaceFieldComponent, ComponentShutdown>(OnFieldShutdown);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;

        foreach (var (session, fields) in _sessionFields)
        {
            foreach (var field in fields)
                _pvs.RemoveSessionOverride(field, session);
        }

        _sessionFields.Clear();
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Disconnected)
            CleanupSession(e.Session);
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (ev.Player.Status == SessionStatus.Disconnected)
            return;

        SyncSession(ev.Player, Transform(ev.Entity).MapID);
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        if (ev.Player.Status == SessionStatus.Disconnected)
        {
            CleanupSession(ev.Player);
            return;
        }

        ClearSessionOverrides(ev.Player);
    }

    private void OnActorParentChanged(Entity<ActorComponent> ent, ref EntParentChangedMessage args)
    {
        var session = ent.Comp.PlayerSession;
        if (session.Status == SessionStatus.Disconnected)
            return;
        if (args.OldMapId == args.Transform.MapUid)
            return;

        SyncSession(session, args.Transform.MapID);
    }

    private void OnGridParentChanged(Entity<MapGridComponent> grid, ref EntParentChangedMessage args)
    {
        if (args.OldMapId == args.Transform.MapUid)
            return;

        var mapId = args.Transform.MapID;
        foreach (var session in _players.Sessions)
        {
            if (session.Status == SessionStatus.Disconnected || session.AttachedEntity is not { } attached)
                continue;
            if (Transform(attached).GridUid != grid.Owner)
                continue;

            SyncSession(session, mapId);
        }
    }

    private void OnFieldMapInit(Entity<AmbientSpaceFieldComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp(ent, out TransformComponent? xform))
            return;

        var mapId = xform.MapID;
        if (mapId == MapId.Nullspace)
            return;

        foreach (var (session, fields) in _sessionFields)
        {
            if (session.Status == SessionStatus.Disconnected || session.AttachedEntity is not { } attached)
                continue;

            if (Transform(attached).MapID != mapId)
                continue;

            if (!fields.Add(ent.Owner))
                continue;

            _pvs.AddSessionOverride(ent.Owner, session);
        }
    }

    private void OnFieldShutdown(Entity<AmbientSpaceFieldComponent> ent, ref ComponentShutdown args)
    {
        foreach (var (session, fields) in _sessionFields)
        {
            if (!fields.Remove(ent.Owner))
                continue;

            _pvs.RemoveSessionOverride(ent.Owner, session);
        }
    }

    private void SyncSession(ICommonSession session, MapId mapId)
    {
        if (!_sessionFields.TryGetValue(session, out var current))
        {
            current = new HashSet<EntityUid>();
            _sessionFields[session] = current;
        }

        CollectDesired(mapId, _desired);

        _removeScratch.Clear();
        foreach (var field in current)
        {
            if (!_desired.Contains(field))
                _removeScratch.Add(field);
        }

        foreach (var field in _removeScratch)
        {
            _pvs.RemoveSessionOverride(field, session);
            current.Remove(field);
        }

        foreach (var field in _desired)
        {
            if (!current.Add(field))
                continue;

            _pvs.AddSessionOverride(field, session);
        }
    }

    private void ClearSessionOverrides(ICommonSession session)
    {
        if (!_sessionFields.TryGetValue(session, out var fields))
            return;

        foreach (var field in fields)
            _pvs.RemoveSessionOverride(field, session);

        fields.Clear();
    }

    private void CleanupSession(ICommonSession session)
    {
        ClearSessionOverrides(session);
        _sessionFields.Remove(session);
    }

    private void CollectDesired(MapId mapId, HashSet<EntityUid> desired)
    {
        desired.Clear();

        if (mapId == MapId.Nullspace)
            return;

        var fields = EntityQueryEnumerator<AmbientSpaceFieldComponent, TransformComponent>();
        while (fields.MoveNext(out var uid, out _, out var fieldXform))
        {
            if (fieldXform.MapID != mapId)
                continue;

            desired.Add(uid);
        }
    }
}
