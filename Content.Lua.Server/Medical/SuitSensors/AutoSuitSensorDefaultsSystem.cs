using Content.Server.Medical.SuitSensors;
using Content.Server._NF.Medical.SuitSensors; //Lua
using Content.Server.Access.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Inventory;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Roles;
using Robust.Shared.Timing;

namespace Content.Lua.Server.Medical.SuitSensors;

public sealed class AutoSuitSensorDefaultsSystem : EntitySystem
{
    [Dependency] private readonly SuitSensorSystem _suitSensors = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;

    public override void Initialize()
    {
        base.Initialize();

            SubscribeLocalEvent<StartingGearEquippedEvent>(OnStartingGearEquipped); //Lua: after starting gear equip - enable sensors for allowed roles
    }

    private void OnStartingGearEquipped(ref StartingGearEquippedEvent args)
    {
        var wearer = args.Entity;

        Timer.Spawn(TimeSpan.FromMilliseconds(100), () =>
        {
            if (HasComp<DisableSuitSensorsComponent>(wearer))
            {
                _suitSensors.SetAllSensors(wearer, SuitSensorMode.SensorOff, SlotFlags.All);
                return;
            }

            if (HasComp<Content.Shared.NukeOps.NukeOperativeComponent>(wearer))
            {
                _suitSensors.SetAllSensors(wearer, SuitSensorMode.SensorOff, SlotFlags.All);
                return;
            }

            if (_idCard.TryFindIdCard(wearer, out var delayedId))
            {
                var jobIconStr = delayedId.Comp.JobIcon.Id;
                if (!string.IsNullOrEmpty(jobIconStr)) //Lua: guard against null/empty
                {
                    if (jobIconStr == "JobIconMercenary")
                    {
                        _suitSensors.SetAllSensors(wearer, SuitSensorMode.SensorOff, SlotFlags.All);
                        return;
                    }
                    if (jobIconStr.StartsWith("JobIconSyndicate"))
                    {
                        _suitSensors.SetAllSensors(wearer, SuitSensorMode.SensorOff, SlotFlags.All);
                        return;
                    }
                    if (jobIconStr.StartsWith("JobIconNFPirate"))
                    {
                        _suitSensors.SetAllSensors(wearer, SuitSensorMode.SensorOff, SlotFlags.All);
                        return;
                    }
                }

                if (TryComp<AccessComponent>(delayedId.Owner, out var delayedAccess))
                {
                    if (delayedAccess.Tags.Contains("Mercenary"))
                    {
                        _suitSensors.SetAllSensors(wearer, SuitSensorMode.SensorOff, SlotFlags.All);
                        return;
                    }
                    if (delayedAccess.Tags.Contains("Syndicate") || delayedAccess.Tags.Contains("NFSyndicate"))
                    {
                        _suitSensors.SetAllSensors(wearer, SuitSensorMode.SensorOff, SlotFlags.All);
                        return;
                    }
                    if (delayedAccess.Tags.Contains("Pirate"))
                    {
                        _suitSensors.SetAllSensors(wearer, SuitSensorMode.SensorOff, SlotFlags.All);
                        return;
                    }
                }
            }

            _suitSensors.SetAllSensors(wearer, SuitSensorMode.SensorVitals, SlotFlags.All);
        });
    }
}

