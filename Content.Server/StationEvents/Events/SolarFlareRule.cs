using Content.Server.GameTicking.Rules.Components;
using Content.Server.Radio;
using Robust.Shared.Random;
using Content.Server.Light.EntitySystems;
using Content.Server.Light.Components;
using Content.Server.StationEvents.Components;
using Content.Shared.Radio.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Map;

namespace Content.Server.StationEvents.Events;

public sealed class SolarFlareRule : StationEventSystem<SolarFlareRuleComponent>
{
    [Dependency] private readonly PoweredLightSystem _poweredLight = default!;
    [Dependency] private readonly SharedDoorSystem _door = default!;

    private float _effectTimer = 0;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);
    }

    protected override void Started(EntityUid uid, SolarFlareRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);
        comp.TargetMap = GetRelevantMapId();

        for (var i = 0; i < comp.ExtraCount; i++)
        {
            var channel = RobustRandom.Pick(comp.ExtraChannels);
            comp.AffectedChannels.Add(channel);
        }
    }

    protected override void ActiveTick(EntityUid uid, SolarFlareRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        _effectTimer -= frameTime;
        if (_effectTimer < 0)
        {
            _effectTimer += 1;
            var lightQuery = EntityQueryEnumerator<PoweredLightComponent, TransformComponent>();
            while (lightQuery.MoveNext(out var lightEnt, out var light, out var lightXform))
            {
                if (component.TargetMap != MapId.Nullspace && lightXform.MapID != component.TargetMap)
                    continue;

                // Frontier: shielded lights
                var prob = component.LightBreakChancePerSecond * light.SolarFlareShieldingCoefficient;
                if (RobustRandom.Prob(prob))
                    _poweredLight.TryDestroyBulb(lightEnt, light);
                // End Frontier: shielded lights
            }
            var airlockQuery = EntityQueryEnumerator<AirlockComponent, DoorComponent, TransformComponent>();
            while (airlockQuery.MoveNext(out var airlockEnt, out var airlock, out var door, out var doorXform))
            {
                if (component.TargetMap != MapId.Nullspace && doorXform.MapID != component.TargetMap)
                    continue;

                if (airlock.AutoClose && RobustRandom.Prob(component.DoorToggleChancePerSecond))
                    _door.TryToggleDoor(airlockEnt, door);
            }
        }
    }

    private void OnRadioReceiveAttempt(ref RadioReceiveAttemptEvent args)
    {
        var query = EntityQueryEnumerator<SolarFlareRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var flare, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            if (!flare.AllChannels && !flare.AffectedChannels.Contains(args.Channel.ID)) // Frontier: add flare.AllChannels
                continue;

            if (flare.TargetMap != MapId.Nullspace)
            {
                var receiverMap = Transform(args.RadioReceiver).MapID;
                if (receiverMap != flare.TargetMap)
                    continue;
            }

            if (!flare.OnlyJamHeadsets || (HasComp<HeadsetComponent>(args.RadioReceiver) || HasComp<HeadsetComponent>(args.RadioSource)))
                args.Cancelled = true;
        }
    }
}
