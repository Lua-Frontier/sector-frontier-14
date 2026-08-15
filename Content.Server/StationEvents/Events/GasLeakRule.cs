using Content.Server.Atmos.EntitySystems;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Station.Components;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Audio;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.StationEvents.Events
{
    internal sealed class GasLeakRule : StationEventSystem<GasLeakRuleComponent>
    {
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly AtmosphereSystem _atmosphere = default!;

        protected override void Started(EntityUid uid, GasLeakRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
        {
            base.Started(uid, component, gameRule, args);

            if (!TryComp<StationEventComponent>(uid, out var stationEvent))
                return;

            if (!TryGetRandomStationForEvent(uid, out var target) || target == null)
                return;

            if (!TryFindRandomTileOnStation((target.Value, Comp<StationDataComponent>(target.Value)),
                    out component.TargetTile,
                    out component.TargetGrid,
                    out component.TargetCoords))
                return;

            component.TargetStation = target.Value;
            component.FoundTile = true;
            component.LeakGas = RobustRandom.Pick(component.LeakableGases);
            var totalGas = RobustRandom.Next(component.MinimumGas, component.MaximumGas);
            component.MolesPerSecond = RobustRandom.Next(component.MinimumMolesPerSecond, component.MaximumMolesPerSecond);

            if (gameRule.Delay is {} startAfter)
                stationEvent.EndTime = _timing.CurTime + TimeSpan.FromSeconds(totalGas / component.MolesPerSecond + startAfter.Next(RobustRandom));
        }

        protected override void ActiveTick(EntityUid uid, GasLeakRuleComponent component, GameRuleComponent gameRule, float frameTime)
        {
            base.ActiveTick(uid, component, gameRule, frameTime);
            component.TimeUntilLeak -= frameTime;

            if (component.TimeUntilLeak > 0f)
                return;
            component.TimeUntilLeak += component.LeakCooldown;

            if (!component.FoundTile ||
                component.TargetGrid == default ||
                Deleted(component.TargetGrid) ||
                !_atmosphere.IsSimulatedGrid(component.TargetGrid))
            {
                ForceEndSelf(uid, gameRule);
                return;
            }

            var environment = _atmosphere.GetTileMixture(component.TargetGrid, null, component.TargetTile, true);

            environment?.AdjustMoles(component.LeakGas, component.LeakCooldown * component.MolesPerSecond);
        }

        protected override void Ended(EntityUid uid, GasLeakRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
        {
            base.Ended(uid, component, gameRule, args);
            Spark(uid, component);
        }

        private void Spark(EntityUid uid, GasLeakRuleComponent component)
        {
            if (RobustRandom.NextFloat() <= component.SparkChance)
            {
                if (!component.FoundTile ||
                    component.TargetGrid == default ||
                    (!Exists(component.TargetGrid) ? EntityLifeStage.Deleted : MetaData(component.TargetGrid).EntityLifeStage) >= EntityLifeStage.Deleted ||
                    !_atmosphere.IsSimulatedGrid(component.TargetGrid))
                {
                    return;
                }

                // Don't want it to be so obnoxious as to instantly murder anyone in the area but enough that
                // it COULD start potentially start a bigger fire.
                _atmosphere.HotspotExpose(component.TargetGrid, component.TargetTile, 700f, 50f, null, true);
                Audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/sparks4.ogg"), component.TargetCoords);
            }
        }
    }
}
