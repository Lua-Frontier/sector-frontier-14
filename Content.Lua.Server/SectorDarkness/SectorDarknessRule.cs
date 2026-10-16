using Content.Lua.Server.Sectors;
using Content.Lua.Shared.SectorDarkness;
using Content.Server.Ghost;
using Content.Server.Light.Components;
using Content.Server.Light.EntitySystems;
using Content.Server.Radio;
using Content.Server.StationEvents;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Lua.Server.SectorDarkness;

public sealed class SectorDarknessRule : StationEventSystem<SectorDarknessRuleComponent>
{
    [Dependency] private readonly SectorSystem _sectors = default!;
    [Dependency] private readonly PoweredLightSystem _poweredLight = default!;
    [Dependency] private readonly SharedDoorSystem _door = default!;
    [Dependency] private readonly EventManagerSystem _events = default!;
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private const float EffectInterval = 10f;

    private MapId _relevantMapId = MapId.Nullspace;
    private MapId _jammedMapId = MapId.Nullspace;
    private float _effectTimer;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);
    }

    protected override MapId GetRelevantMapId()
    {
        return _relevantMapId;
    }

    protected override void Added(EntityUid uid, SectorDarknessRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        if (TryComp<StationEventComponent>(uid, out var stationEvent) && _events.IsBlocked(uid, stationEvent))
        {
            component.Blocked = true;
            Sawmill.Info($"{ToPrettyString(uid)} blocked by another active event");
            GameTicker.EndGameRule(uid, gameRule);
            return;
        }

        if (!_sectors.TryGetMapId(component.Sector, out var mapId))
            mapId = _sectors.ResolveHubMapId();

        component.MapId = mapId;
        _relevantMapId = mapId;
        base.Added(uid, component, gameRule, args);
    }

    protected override void Started(EntityUid uid, SectorDarknessRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        _relevantMapId = component.MapId;
        base.Started(uid, component, gameRule, args);

        if (component.MapId == MapId.Nullspace || !MapSystem.MapExists(component.MapId))
            return;

        var map = MapSystem.GetMap(component.MapId);
        component.Map = map;
        _jammedMapId = component.MapId;
        _effectTimer = EffectInterval;

        var darkness = EnsureComp<SectorDarknessComponent>(map);
        darkness.Music = component.Music;
        Dirty(map, darkness);
    }

    protected override void Ended(EntityUid uid, SectorDarknessRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        if (component.Blocked)
            return;

        _relevantMapId = component.MapId;
        base.Ended(uid, component, gameRule, args);

        if (component.Map is not { } map)
            return;

        _jammedMapId = MapId.Nullspace;
        RemComp<SectorDarknessComponent>(map);
    }

    protected override void ActiveTick(EntityUid uid, SectorDarknessRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        _effectTimer -= frameTime;
        if (_effectTimer > 0f)
            return;

        _effectTimer += EffectInterval;
        var lightChance = component.LightBreakChancePerSecond * EffectInterval;
        var doorChance = component.DoorToggleChancePerSecond * EffectInterval;

        var flickerChance = component.LightFlickerChancePerSecond * EffectInterval;
        var messageChance = component.MessageChancePerSecond * EffectInterval;

        var lights = EntityQueryEnumerator<PoweredLightComponent, TransformComponent>();
        while (lights.MoveNext(out var lightUid, out var light, out var xform))
        {
            if (xform.MapID != component.MapId)
                continue;

            if (RobustRandom.Prob(lightChance * light.SolarFlareShieldingCoefficient))
                _poweredLight.TryDestroyBulb(lightUid, light);
            else if (RobustRandom.Prob(flickerChance))
                _ghost.DoGhostBooEvent(lightUid);
        }

        if (component.Messages.Count > 0)
        {
            var players = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
            while (players.MoveNext(out var playerUid, out _, out _, out var xform))
            {
                if (xform.MapID == component.MapId && RobustRandom.Prob(messageChance))
                    _popup.PopupEntity(Loc.GetString(RobustRandom.Pick(component.Messages)), playerUid, playerUid);
            }
        }

        var airlocks = EntityQueryEnumerator<AirlockComponent, DoorComponent, TransformComponent>();
        while (airlocks.MoveNext(out var airlockUid, out var airlock, out var door, out var xform))
        {
            if (xform.MapID == component.MapId && airlock.AutoClose && RobustRandom.Prob(doorChance))
                _door.TryToggleDoor(airlockUid, door);
        }
    }

    private void OnRadioReceiveAttempt(ref RadioReceiveAttemptEvent args)
    {
        if (_jammedMapId != MapId.Nullspace && Transform(args.RadioReceiver).MapID == _jammedMapId)
            args.Cancelled = true;
    }
}
