using System.Numerics;
using Content.Shared.Light.Components;
using Content.Shared.Weather;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using AudioComponent = Robust.Shared.Audio.Components.AudioComponent;

namespace Content.Client.Weather;

public sealed class WeatherSystem : SharedWeatherSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _streams = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WeatherComponent, ComponentHandleState>(OnWeatherHandleState);
        SubscribeLocalEvent<WeatherComponent, ComponentShutdown>(OnWeatherShutdown);
        SubscribeLocalEvent<WeatherComponent, EntityPausedEvent>(OnWeatherPaused);
    }

    private void OnWeatherShutdown(EntityUid uid, WeatherComponent component, ComponentShutdown args)
    {
        StopComponentStreams(component);
        StopOrphanStreams();
    }

    private void OnWeatherPaused(EntityUid uid, WeatherComponent component, ref EntityPausedEvent args)
    {
        StopComponentStreams(component);
        StopOrphanStreams();
    }

    private void ForceStop(ref EntityUid? stream)
    {
        if (stream is not { } uid)
            return;

        stream = null;
        _streams.Remove(uid);
        if (!Deleted(uid))
            QueueDel(uid);
    }

    private EntityUid? PlayWeatherSound(SoundSpecifier sound)
    {
        var played = _audio.PlayGlobal(sound, Filter.Local(), true);
        if (played == null)
            return null;

        _streams.Add(played.Value.Entity);
        return played.Value.Entity;
    }

    private void StopComponentStreams(WeatherComponent component)
    {
        foreach (var weather in component.Weather.Values)
        {
            var stream = weather.Stream;
            ForceStop(ref stream);
            weather.Stream = stream;
        }
    }

    private void StopOrphanStreams()
    {
        foreach (var uid in _streams)
        {
            if (!Deleted(uid))
                QueueDel(uid);
        }

        _streams.Clear();
    }

    private void StopWeatherAudioIfPlayerAway()
    {
        var ent = _playerManager.LocalEntity;
        EntityUid? playerMap = null;
        if (ent != null)
            playerMap = Transform(ent.Value).MapUid;

        if (playerMap == null
            || !TryComp<WeatherComponent>(playerMap.Value, out var active)
            || active.Weather.Count == 0)
        {
            var query = EntityManager.AllEntityQueryEnumerator<WeatherComponent>();
            while (query.MoveNext(out _, out var comp))
                StopComponentStreams(comp);

            StopOrphanStreams();
            return;
        }

        var keep = new HashSet<EntityUid>();
        foreach (var weather in active.Weather.Values)
        {
            if (weather.Stream != null)
                keep.Add(weather.Stream.Value);
        }

        foreach (var uid in _streams)
        {
            if (keep.Contains(uid) || Deleted(uid))
                continue;

            QueueDel(uid);
        }

        _streams.RemoveWhere(u => Deleted(u) || !keep.Contains(u));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        StopWeatherAudioIfPlayerAway();
    }

    protected override void EndWeather(EntityUid uid, WeatherComponent component, string proto)
    {
        if (!component.Weather.TryGetValue(proto, out var data))
            return;

        var stream = data.Stream;
        ForceStop(ref stream);
        data.Stream = null;
        component.Weather.Remove(proto);
        Dirty(uid, component);
    }

    protected override void Run(EntityUid uid, WeatherData weather, WeatherPrototype weatherProto, float frameTime)
    {
        base.Run(uid, weather, weatherProto, frameTime);

        var ent = _playerManager.LocalEntity;
        if (ent == null)
        {
            var stream = weather.Stream;
            ForceStop(ref stream);
            weather.Stream = stream;
            return;
        }

        var mapUid = Transform(uid).MapUid ?? uid;
        var entXform = Transform(ent.Value);

        if (entXform.MapUid != mapUid && entXform.MapUid != uid)
        {
            var stream = weather.Stream;
            ForceStop(ref stream);
            weather.Stream = stream;
            return;
        }

        if (!Timing.IsFirstTimePredicted || weatherProto.Sound == null)
            return;

        if (weather.Stream == null || Deleted(weather.Stream.Value))
        {
            weather.Stream = PlayWeatherSound(weatherProto.Sound);
            if (weather.Stream != null)
                _streams.Add(weather.Stream.Value);
        }

        if (!TryComp(weather.Stream, out AudioComponent? comp))
            return;

        var occlusion = 0f;

        if (TryComp<MapGridComponent>(entXform.GridUid, out var grid))
        {
            TryComp(entXform.GridUid, out RoofComponent? roofComp);
            var gridId = entXform.GridUid.Value;
            var seed = _mapSystem.GetTileRef(gridId, grid, entXform.Coordinates);
            var frontier = new Queue<TileRef>();
            frontier.Enqueue(seed);
            EntityCoordinates? nearestNode = null;
            var visited = new HashSet<Vector2i>();

            while (frontier.TryDequeue(out var node))
            {
                if (!visited.Add(node.GridIndices))
                    continue;

                if (!CanWeatherAffect(entXform.GridUid.Value, grid, node, roofComp))
                {
                    for (var x = -1; x <= 1; x++)
                    {
                        for (var y = -1; y <= 1; y++)
                        {
                            if (Math.Abs(x) == 1 && Math.Abs(y) == 1 ||
                                x == 0 && y == 0 ||
                                (new Vector2(x, y) + node.GridIndices - seed.GridIndices).Length() > 3)
                            {
                                continue;
                            }

                            frontier.Enqueue(_mapSystem.GetTileRef(gridId, grid, new Vector2i(x, y) + node.GridIndices));
                        }
                    }

                    continue;
                }

                nearestNode = new EntityCoordinates(entXform.GridUid.Value,
                    node.GridIndices + grid.TileSizeHalfVector);
                break;
            }

            if (nearestNode != null)
            {
                var entPos = _transform.GetMapCoordinates(entXform);
                var nodePosition = _transform.ToMapCoordinates(nearestNode.Value).Position;
                var delta = nodePosition - entPos.Position;
                var distance = delta.Length();
                occlusion = _audio.GetOcclusion(entPos, delta, distance);
            }
            else
            {
                occlusion = 3f;
            }
        }

        var alpha = GetPercent(weather, uid);
        alpha *= SharedAudioSystem.VolumeToGain(weatherProto.Sound.Params.Volume);
        _audio.SetGain(weather.Stream, alpha, comp);
        comp.Occlusion = occlusion;
    }

    protected override bool SetState(EntityUid uid, WeatherState state, WeatherComponent comp, WeatherData weather, WeatherPrototype weatherProto)
    {
        if (!base.SetState(uid, state, comp, weather, weatherProto))
            return false;

        if (!Timing.IsFirstTimePredicted)
            return true;

        var stream = weather.Stream;
        ForceStop(ref stream);
        weather.Stream = stream;

        var ent = _playerManager.LocalEntity;
        if (ent == null || Transform(ent.Value).MapUid != uid)
            return true;

        if (weatherProto.Sound != null)
            weather.Stream = PlayWeatherSound(weatherProto.Sound);

        return true;
    }

    private void OnWeatherHandleState(EntityUid uid, WeatherComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not WeatherComponentState state)
            return;

        foreach (var (proto, weather) in component.Weather)
        {
            if (!state.Weather.TryGetValue(proto, out var stateData))
            {
                EndWeather(uid, component, proto);
                continue;
            }

            weather.StartTime = stateData.StartTime;
            weather.EndTime = stateData.EndTime;
            weather.State = stateData.State;
        }

        foreach (var (proto, weather) in state.Weather)
        {
            if (component.Weather.ContainsKey(proto))
                continue;

            StartWeather(uid, component, ProtoMan.Index<WeatherPrototype>(proto), weather.EndTime);
        }
    }
}
