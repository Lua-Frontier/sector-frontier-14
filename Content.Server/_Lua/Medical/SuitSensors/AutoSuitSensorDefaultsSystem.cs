using Content.Server.GameTicking;
using Content.Server.Medical.SuitSensors;
using Content.Server._NF.Medical.SuitSensors; //Lua
using Content.Server.Access.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Inventory;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Roles;
using Robust.Shared.Timing;

namespace Content.Server._Lua.Medical.SuitSensors;

/// <summary>
/// Sets default behavior for suit sensors:
/// - For any clothing with sensors spawned during the round, default to Coordinates mode.
/// - On roundstart starting gear equip, turn sensors ON for everyone except pirates, syndicates, and mercenaries.
/// </summary>
public sealed class AutoSuitSensorDefaultsSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _ticker = default!;
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

        //Lua: delay so job specials/components are applied before checking exclusions
        Timer.Spawn(TimeSpan.FromMilliseconds(100), () =>
        {
            //Lua: pirates - force sensors OFF
            if (HasComp<DisableSuitSensorsComponent>(wearer))
            {
                _suitSensors.SetAllSensors(wearer, SuitSensorMode.SensorOff, SlotFlags.All);
                return;
            }

            //Lua: nuke ops/syndicate operatives - sensors OFF
            if (HasComp<Content.Shared.NukeOps.NukeOperativeComponent>(wearer))
            {
                _suitSensors.SetAllSensors(wearer, SuitSensorMode.SensorOff, SlotFlags.All);
                return;
            }

            //Lua: exclude by job icon/access tags (Syndicate/Pirate/Merc)
            if (_idCard.TryFindIdCard(wearer, out var delayedId))
            {
                //Lua: job icon check using string id
                var jobIconStr = delayedId.Comp.JobIcon.Id;
                if (!string.IsNullOrEmpty(jobIconStr)) //Lua: guard against null/empty
                {
                    //Lua: mercenary
                    if (jobIconStr == "JobIconMercenary")
                    {
                        _suitSensors.SetAllSensors(wearer, SuitSensorMode.SensorOff, SlotFlags.All);
                        return;
                    }
                    //Lua: any syndicate icon (prefix match)
                    if (jobIconStr.StartsWith("JobIconSyndicate"))
                    {
                        _suitSensors.SetAllSensors(wearer, SuitSensorMode.SensorOff, SlotFlags.All);
                        return;
                    }
                    //Lua: any pirate icon (prefix match)
                    if (jobIconStr.StartsWith("JobIconNFPirate"))
                    {
                        _suitSensors.SetAllSensors(wearer, SuitSensorMode.SensorOff, SlotFlags.All);
                        return;
                    }
                }

                //Lua: access tag check (Mercenary/Syndicate/NFSyndicate/Pirate)
                if (TryComp<AccessComponent>(delayedId.Owner, out var delayedAccess))
                {
                    //Lua: mercenary
                    if (delayedAccess.Tags.Contains("Mercenary"))
                    {
                        _suitSensors.SetAllSensors(wearer, SuitSensorMode.SensorOff, SlotFlags.All);
                        return;
                    }
                    //Lua: syndicate
                    if (delayedAccess.Tags.Contains("Syndicate") || delayedAccess.Tags.Contains("NFSyndicate"))
                    {
                        _suitSensors.SetAllSensors(wearer, SuitSensorMode.SensorOff, SlotFlags.All);
                        return;
                    }
                    //Lua: pirates
                    if (delayedAccess.Tags.Contains("Pirate"))
                    {
                        _suitSensors.SetAllSensors(wearer, SuitSensorMode.SensorOff, SlotFlags.All);
                        return;
                    }
                }
            }

            //Lua: default for roundstart - turn sensors ON (Vitals) for everyone else
            _suitSensors.SetAllSensors(wearer, SuitSensorMode.SensorVitals, SlotFlags.All);
        });
    }
}



