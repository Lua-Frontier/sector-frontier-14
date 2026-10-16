using Content.Lua.Shared.Sectors; // Lua
using Content.Server.Silicons.Laws;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Silicons.Laws.Components;
using Robust.Shared.Map;
using Robust.Shared.Random; // Lua

namespace Content.Server.StationEvents.Events;

public sealed class IonStormRule : StationEventSystem<IonStormRuleComponent>
{
    [Dependency] private readonly IonStormSystem _ionStorm = default!;
    [Dependency] private readonly ISectorSystem _sectors = default!; // Lua

    // Lua start
    private MapId _relevantMapId = MapId.Nullspace;

    protected override MapId GetRelevantMapId()
    {
        return _relevantMapId;
    }

    protected override void Added(EntityUid uid, IonStormRuleComponent comp, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        var maps = new List<MapId>();
        foreach (var sector in comp.Sectors)
        {
            if (_sectors.TryGetMapId(sector, out var mapId))
                maps.Add(mapId);
        }

        _relevantMapId = maps.Count > 0 ? RobustRandom.Pick(maps) : _sectors.ResolveHubMapId();
        base.Added(uid, comp, gameRule, args);
    }
    // Lua end

    protected override void Started(EntityUid uid, IonStormRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        var query = EntityQueryEnumerator<SiliconLawBoundComponent, TransformComponent, IonStormTargetComponent>();
        while (query.MoveNext(out var ent, out var lawBound, out var xform, out var target))
        {
            if (_relevantMapId != MapId.Nullspace && xform.MapID != _relevantMapId) // Lua
                continue;

            _ionStorm.IonStormTarget((ent, lawBound, target));
        }
    }
}
