using Content.Lua.Server.Sectors;
using Content.Lua.Shared.SectorBrightness;
using Content.Server.Light.Components;
using Content.Server.StationEvents;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Lua.Server.SectorBrightness;

public sealed class SectorBrightnessRule : StationEventSystem<SectorBrightnessRuleComponent>
{
    [Dependency] private readonly SectorSystem _sectors = default!;
    [Dependency] private readonly EventManagerSystem _events = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;

    private const float EffectInterval = 10f;

    /// <summary>
    /// Clients fade the ambient light out on their own, so the map light is kept a bit longer than that.
    /// </summary>
    private static readonly TimeSpan MapLightRemoveDelay = TimeSpan.FromSeconds(15);

    private MapId _relevantMapId = MapId.Nullspace;
    private float _effectTimer;

    protected override MapId GetRelevantMapId()
    {
        return _relevantMapId;
    }

    protected override void Added(EntityUid uid, SectorBrightnessRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
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

    protected override void Started(EntityUid uid, SectorBrightnessRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        _relevantMapId = component.MapId;
        base.Started(uid, component, gameRule, args);

        if (component.MapId == MapId.Nullspace || !MapSystem.MapExists(component.MapId))
            return;

        var map = MapSystem.GetMap(component.MapId);
        component.Map = map;
        _effectTimer = EffectInterval;
        component.AddedMapLight = !HasComp<MapLightComponent>(map);
        EnsureComp<MapLightComponent>(map);

        var brightness = EnsureComp<SectorBrightnessComponent>(map);
        brightness.Music = component.Music;
        brightness.AmbientColor = component.AmbientColor;
        Dirty(map, brightness);

        BoostLights(component);
    }

    protected override void Ended(EntityUid uid, SectorBrightnessRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        if (component.Blocked)
            return;

        _relevantMapId = component.MapId;
        base.Ended(uid, component, gameRule, args);

        if (component.Map is not { } map)
            return;

        RestoreLights(component);
        RemComp<SectorBrightnessComponent>(map);
        if (!component.AddedMapLight)
            return;

        Robust.Shared.Timing.Timer.Spawn(MapLightRemoveDelay, () =>
        {
            if (!TerminatingOrDeleted(map) && !HasComp<SectorBrightnessComponent>(map))
                RemComp<MapLightComponent>(map);
        });
    }

    protected override void ActiveTick(EntityUid uid, SectorBrightnessRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        _effectTimer -= frameTime;
        if (_effectTimer > 0f)
            return;

        _effectTimer += EffectInterval;
        if (component.Messages.Count == 0)
            return;

        var messageChance = component.MessageChancePerSecond * EffectInterval;
        var players = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (players.MoveNext(out var playerUid, out _, out _, out var xform))
        {
            if (xform.MapID == component.MapId && RobustRandom.Prob(messageChance))
                _popup.PopupEntity(Loc.GetString(RobustRandom.Pick(component.Messages)), playerUid, playerUid);
        }
    }

    private void BoostLights(SectorBrightnessRuleComponent component)
    {
        if (component.LightEnergyMultiplier == 1f && component.LightSaturationMultiplier == 1f)
            return;

        var lights = EntityQueryEnumerator<PoweredLightComponent, TransformComponent>();
        while (lights.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID != component.MapId || !_pointLight.TryGetLight(uid, out var light) || !light.Enabled)
                continue;

            var hsv = Color.ToHsv(light.Color);
            hsv.Y = Math.Clamp(hsv.Y * component.LightSaturationMultiplier, 0f, 1f);
            var energy = light.Energy * component.LightEnergyMultiplier;

            component.BoostedLights[uid] = (light.Energy, light.Color, energy);
            _pointLight.SetEnergy(uid, energy, light);
            _pointLight.SetColor(uid, Color.FromHsv(hsv), light);
        }
    }

    private void RestoreLights(SectorBrightnessRuleComponent component)
    {
        foreach (var (uid, (energy, color, boosted)) in component.BoostedLights)
        {
            // Lights that were toggled or replaced in the meantime already have their own values.
            if (!_pointLight.TryGetLight(uid, out var light) || !MathHelper.CloseTo(light.Energy, boosted))
                continue;

            _pointLight.SetEnergy(uid, energy, light);
            _pointLight.SetColor(uid, color, light);
        }

        component.BoostedLights.Clear();
    }
}
