using Content.Lua.Server.ShipTracker.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.RoundEnd;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking.Components;

namespace Content.Lua.Server.ShipTracker.Rules.EndOnShipDestruction;

public sealed class EndOnShipDestructionSystem : GameRuleSystem<EndOnShipDestructionComponent>
{
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawningEvent);
    }

    private void OnPlayerSpawningEvent(PlayerSpawningEvent ev)
    {
        var query = EntityQueryEnumerator<EndOnShipDestructionComponent, GameRuleComponent>();

        while (query.MoveNext(out var eosComponent, out _))
        {
            if (!ev.Station.HasValue)
            {
                Log.Fatal("Unable to get station on player spawning, does it exist?");
                _roundEndSystem.EndRound();
                return;
            }

            if (!TryComp<StationDataComponent>(ev.Station.Value, out var stationDataComponent))
            {
                Log.Fatal("Failed to get station data!");
                _roundEndSystem.EndRound();
                return;
            }

            var entity = _stationSystem.GetLargestGrid(stationDataComponent);
            if (!entity.HasValue)
            {
                Log.Fatal("Failed to get largest station grid!");
                _roundEndSystem.EndRound();
                return;
            }

            eosComponent.MainShip = entity.Value;
        }
    }

    protected override void AppendRoundEndText(EntityUid uid, EndOnShipDestructionComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        args.AddLine(Loc.GetString("ftl-gamerule-end-text"));
    }

    protected override void ActiveTick(EntityUid uid, EndOnShipDestructionComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (!TryComp<ShipTrackerComponent>(component.MainShip, out var trackerComponent))
        {
            Log.Warning("Ship tracker does not exist on main grid!");
            return;
        }

        if (trackerComponent.Destroyed)
            _roundEndSystem.EndRound();
    }
}
