using Content.Server._Lua.Sectors;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Components;
using Content.Shared._NF.CCVar;
using Content.Shared.Radio;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Server.GameTicking; // Intentionally colliding namespaces to extend the class

public sealed partial class GameTicker
{
    [Dependency] private readonly PlayTimeTrackingManager _playTimeManager = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    private bool _newPlayerGreetingEnabled = true;
    private TimeSpan _newPlayerGreetingMaxTime = TimeSpan.FromMinutes(180);
    private ProtoId<RadioChannelPrototype> _newPlayerRadioChannel = "Service";
    private EntProtoId _greetingRadioSource = "GreetingRadioSource";
    private EntityUid _greetingEntity = EntityUid.Invalid;

    public void NFInitialize()
    {
        Subs.CVar(_cfg, NFCCVars.NewPlayerRadioGreetingEnabled, e => _newPlayerGreetingEnabled = e, true);
        Subs.CVar(_cfg, NFCCVars.NewPlayerRadioGreetingMaxPlaytime, e => _newPlayerGreetingMaxTime = TimeSpan.FromMinutes(e), true);
        Subs.CVar(_cfg, NFCCVars.NewPlayerRadioGreetingChannel, SetChannel, true);
    }

    private void SetChannel(string channel)
    {
        if (_prototypeManager.HasIndex<RadioChannelPrototype>(channel))
            _newPlayerRadioChannel = channel;
    }

    private void NFRoundStarted()
    {
        MapId mapId = MapId.Nullspace;
        var sectors = EntityManager.System<SectorSystem>();
        if (sectors.TryGetHubMapId(out var hubMap) && _map.MapExists(hubMap))
        {
            mapId = hubMap;
        }
        else
        {
            var query = EntityQueryEnumerator<StationDataComponent, TransformComponent>();
            while (query.MoveNext(out _, out _, out var xform))
            {
                if (xform.MapID != MapId.Nullspace && _map.MapExists(xform.MapID))
                {
                    mapId = xform.MapID;
                    break;
                }
            }
        }

        if (mapId == MapId.Nullspace)
            return;

        _greetingEntity = Spawn(_greetingRadioSource, new MapCoordinates(Vector2.Zero, mapId));
    }

    private void NFRoundRestartCleanup()
    {
        if (_greetingEntity != EntityUid.Invalid)
        {
            QueueDel(_greetingEntity);
            _greetingEntity = EntityUid.Invalid;
        }
    }

    private void HandleGreetingMessage(ICommonSession session, EntityUid mob, EntityUid station)
    {
        if (!_newPlayerGreetingEnabled)
            return;

        TimeSpan playtime;
        try
        {
            playtime = _playTimeManager.GetOverallPlaytime(session);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        if (playtime < _newPlayerGreetingMaxTime)
        {
            if (_greetingEntity == EntityUid.Invalid || !EntityManager.EntityExists(_greetingEntity))
                return;

            _radio.SendRadioMessage(_greetingEntity, Loc.GetString("latejoin-arrival-new-player-announcement",
                    ("character", MetaData(mob).EntityName),
                    ("station", station)),
                    _newPlayerRadioChannel,
                    _greetingEntity);
        }
    }
}
