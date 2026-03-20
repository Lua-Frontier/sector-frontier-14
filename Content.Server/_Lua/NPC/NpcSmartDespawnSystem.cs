using Content.Server._Lua.Stargate.Components;
using Content.Server.NPC.HTN;
using Content.Shared.Ghost;
using Content.Shared.Lua.CLVar;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Lua.NPC;
public sealed class NpcSmartDespawnSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly NpcFactionSystem _factionSystem = default!;
    private static readonly ProtoId<NpcFactionPrototype> NanoTrasenFaction = "NanoTrasen";
    private readonly Dictionary<EntityUid, float> _sleepTimers = new();
    private readonly Dictionary<EntityUid, float> _deadTimers = new();
    private bool _enabled;
    private float _sleepTimeout;
    private float _deadTimeout;
    private float _checkInterval;
    private float _timer;
    private EntityQuery<ActiveNPCComponent> _activeQuery;
    private EntityQuery<MobStateComponent> _mobStateQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<GhostComponent> _ghostQuery;
    private readonly HashSet<MapId> _protectedPlanetMaps = new();
    private readonly HashSet<MapId> _planetMapIdsBuffer = new();
    private readonly List<EntityUid> _toRemoveBuffer = new();

    public override void Initialize()
    {
        base.Initialize();
        _activeQuery = GetEntityQuery<ActiveNPCComponent>();
        _mobStateQuery = GetEntityQuery<MobStateComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _ghostQuery = GetEntityQuery<GhostComponent>();
        Subs.CVar(_cfg, CLVars.NpcSmartDespawnEnabled, v => _enabled = v, true);
        Subs.CVar(_cfg, CLVars.NpcSmartDespawnSleepTimeout, v => _sleepTimeout = v, true);
        Subs.CVar(_cfg, CLVars.NpcSmartDespawnDeadTimeout, v => _deadTimeout = v, true);
        Subs.CVar(_cfg, CLVars.NpcSmartDespawnCheckInterval, v => _checkInterval = v, true);
    }

    public override void Update(float frameTime)
    {
        if (!_enabled) return;
        _timer += frameTime;
        if (_timer < _checkInterval) return;
        var elapsed = _timer;
        _timer = 0f;
        RebuildProtectedPlanetMaps();
        var toRemove = _toRemoveBuffer;
        toRemove.Clear();
        foreach (var (uid, deadTime) in _deadTimers)
        {
            if (TerminatingOrDeleted(uid))
            {
                toRemove.Add(uid);
                continue;
            }
            if (_mobStateQuery.TryGetComponent(uid, out var state) && state.CurrentState != MobState.Dead)
            {
                toRemove.Add(uid);
                if (IsHostile(uid)) _sleepTimers[uid] = 0f;
                continue;
            }
            var newTime = deadTime + elapsed;
            if (newTime >= _deadTimeout)
            {
                QueueDel(uid);
                toRemove.Add(uid);
            }
            else
            { _deadTimers[uid] = newTime; }
        }
        foreach (var uid in toRemove)
        {
            _deadTimers.Remove(uid);
            _sleepTimers.Remove(uid);
        }

        toRemove.Clear();
        foreach (var (uid, sleepTime) in _sleepTimers)
        {
            if (TerminatingOrDeleted(uid))
            {
                toRemove.Add(uid);
                continue;
            }
            if (_mobStateQuery.TryGetComponent(uid, out var state) && state.CurrentState == MobState.Dead)
            {
                toRemove.Add(uid);
                _deadTimers.TryAdd(uid, 0f);
                continue;
            }
            if (_activeQuery.HasComp(uid))
            {
                _sleepTimers[uid] = 0f;
                continue;
            }
            if (_xformQuery.TryGetComponent(uid, out var xform) && _protectedPlanetMaps.Contains(xform.MapID))
            {
                _sleepTimers[uid] = 0f;
                continue;
            }
            var newTime = sleepTime + elapsed;
            if (newTime >= _sleepTimeout)
            {
                QueueDel(uid);
                toRemove.Add(uid);
            }
            else { _sleepTimers[uid] = newTime; }
        }
        foreach (var uid in toRemove) _sleepTimers.Remove(uid);
        var query = EntityQueryEnumerator<HTNComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_sleepTimers.ContainsKey(uid) || _deadTimers.ContainsKey(uid)) continue;
            var isDead = _mobStateQuery.TryGetComponent(uid, out var mobState) && mobState.CurrentState == MobState.Dead;
            if (isDead)
            { _deadTimers[uid] = elapsed; }
            else if (IsHostile(uid))
            { _sleepTimers[uid] = _activeQuery.HasComp(uid) ? 0f : elapsed; }
        }
    }
    private void RebuildProtectedPlanetMaps()
    {
        _protectedPlanetMaps.Clear();
        var planetMapIds = _planetMapIdsBuffer;
        planetMapIds.Clear();
        var destQuery = EntityQueryEnumerator<StargateDestinationComponent, TransformComponent>();
        while (destQuery.MoveNext(out _, out _, out var destXform))
        { if (destXform.MapID != MapId.Nullspace) planetMapIds.Add(destXform.MapID); }
        if (planetMapIds.Count == 0) return;
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame) continue;
            if (session.AttachedEntity is not { Valid: true } playerEnt) continue;
            if (_ghostQuery.HasComp(playerEnt)) continue;
            if (!_xformQuery.TryGetComponent(playerEnt, out var xform)) continue;
            if (planetMapIds.Contains(xform.MapID)) _protectedPlanetMaps.Add(xform.MapID);
        }
    }

    private bool IsHostile(EntityUid uid)
    { return _factionSystem.IsFactionHostile(NanoTrasenFaction, (uid, null)); }
}
