using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Shared._Lua.AiShuttle;
using Robust.Shared.Map;

namespace Content.Server._Lua.AiShuttle;

public partial class AiShuttleBrainSystem
{
    private void OnKillSwitchEntityTerminating(Entity<AiShuttleKillSwitchComponent> ent, ref EntityTerminatingEvent args)
    {
        var xform = Transform(ent.Owner);
        if (xform.GridUid is not { } grid) return;
        StopAiOnGrid(grid);
    }

    private void ProcessKillSwitch(EntityUid gridUid, ref AiShuttleBrainComponent brain, TransformComponent xform, float frameTime)
    {
        var killSwitchQuery = EntityQueryEnumerator<AiShuttleKillSwitchComponent, TransformComponent>();
        while (killSwitchQuery.MoveNext(out var killSwitchUid, out var killSwitch, out var killSwitchXform))
        {
            if (killSwitchXform.GridUid != gridUid) continue;
            var currentTime = (float)_gameTiming.CurTime.TotalSeconds;
            if (killSwitch.IsActive)
            {
                killSwitch.TimeRemaining -= frameTime;
                if (killSwitch.TimeRemaining <= 0f)
                { TriggerKillSwitchDestruction(killSwitchUid, killSwitch, gridUid); return; }
            }
            if (currentTime - killSwitch.LastConsoleCheckTime < killSwitch.ConsoleCheckIntervalSeconds) continue;
            killSwitch.LastConsoleCheckTime = currentTime;
            bool consoleFound = false;
            int consoleCount = 0;
            int poweredConsoleCount = 0;
            var consoleQ = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent, ApcPowerReceiverComponent>();
            while (consoleQ.MoveNext(out var consoleUid, out _, out var consoleXform, out var powerReceiver))
            {
                if (consoleXform.GridUid == gridUid)
                {
                    consoleCount++;
                    if (powerReceiver.Powered)
                    {
                        poweredConsoleCount++;
                        consoleFound = true; break;
                    }
                }
            }
            if (consoleFound)
            {
                killSwitch.IsActive = false;
                killSwitch.TimeRemaining = 0f;
                killSwitch.LastConsoleFoundTime = currentTime; continue;
            }
            if (!killSwitch.IsActive)
            {
                killSwitch.IsActive = true;
                killSwitch.TimeRemaining = killSwitch.SelfDestructTimeoutSeconds;
            }
        }
    }

    private void TriggerKillSwitchDestruction(EntityUid killSwitchUid, AiShuttleKillSwitchComponent killSwitch, EntityUid gridUid)
    {
        var killSwitchXform = Transform(killSwitchUid);
        var coords = _xform.GetWorldPosition(killSwitchXform);
        var mapCoords = new MapCoordinates(coords, killSwitchXform.MapID);
        _explosion.QueueExplosion(mapCoords, "Default", killSwitch.ExplosionIntensity, killSwitch.ExplosionSlope, killSwitch.MaxExplosionIntensity, killSwitchUid, tileBreakScale: 1f, maxTileBreak: int.MaxValue, canCreateVacuum: true, addLog: true);
        StopAiOnGrid(gridUid);
    }
}
