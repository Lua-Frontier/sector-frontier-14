using Content.Server._Lua.Sectors;
using Content.Shared._NF.SectorServices.Prototypes;
using Content.Shared.GameTicking;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._NF.SectorServices;

[PublicAPI]
public sealed class SectorServiceSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SectorSystem _sectors = default!;

    private readonly Dictionary<MapId, EntityUid> _servicesByMap = new();
    private readonly Dictionary<EntityUid, MapId> _hostMaps = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationSectorServiceHostComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<StationSectorServiceHostComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
    }

    private void OnComponentInit(EntityUid uid, StationSectorServiceHostComponent component, ComponentInit args)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.MapID == MapId.Nullspace)
            return;

        var mapId = xform.MapID;
        _hostMaps[uid] = mapId;

        if (_servicesByMap.TryGetValue(mapId, out var existing) && EntityManager.EntityExists(existing))
        {
            component.SectorUid = existing;
            return;
        }

        var service = Spawn();
        component.SectorUid = service;
        _servicesByMap[mapId] = service;

        foreach (var servicePrototype in _prototypeManager.EnumeratePrototypes<SectorServicePrototype>())
            EntityManager.AddComponents(service, servicePrototype.Components, false);
    }

    private void OnComponentRemove(EntityUid uid, StationSectorServiceHostComponent component, ComponentRemove args)
    {
        if (!_hostMaps.Remove(uid, out var mapId))
            return;

        foreach (var remaining in _hostMaps.Values)
        {
            if (remaining == mapId)
                return;
        }

        DeleteServiceForMap(mapId);
    }

    public void OnCleanup(RoundRestartCleanupEvent _)
    {
        var maps = new List<MapId>(_servicesByMap.Keys);
        foreach (var mapId in maps)
            DeleteServiceForMap(mapId);

        _servicesByMap.Clear();
        _hostMaps.Clear();
    }

    private void DeleteServiceForMap(MapId mapId)
    {
        if (!_servicesByMap.Remove(mapId, out var service))
            return;

        if (EntityManager.EntityExists(service) && !Terminating(service))
            QueueDel(service);
    }

    public EntityUid GetServiceEntity()
    {
        if (_sectors.TryGetHubMapId(out var hubMap) &&
            hubMap != MapId.Nullspace &&
            _servicesByMap.TryGetValue(hubMap, out var hub) &&
            EntityManager.EntityExists(hub))
            return hub;

        foreach (var service in _servicesByMap.Values)
        {
            if (EntityManager.EntityExists(service))
                return service;
        }

        return EntityUid.Invalid;
    }

    public bool TryGetServiceEntity(MapId mapId, out EntityUid service)
    {
        if (mapId != MapId.Nullspace && _servicesByMap.TryGetValue(mapId, out var found) && EntityManager.EntityExists(found))
        {
            service = found;
            return true;
        }

        service = default;
        return false;
    }

    public bool TryGetServiceEntity(EntityUid context, out EntityUid service)
    {
        if (!TryComp(context, out TransformComponent? xform))
        {
            service = default;
            return false;
        }

        return TryGetServiceEntity(xform.MapID, out service);
    }

    public IEnumerable<EntityUid> GetServiceEntities()
    {
        foreach (var service in _servicesByMap.Values)
        {
            if (EntityManager.EntityExists(service))
                yield return service;
        }
    }

    public bool TryGetMapId(EntityUid service, out MapId mapId)
    {
        foreach (var (map, ent) in _servicesByMap)
        {
            if (ent != service)
                continue;
            mapId = map;
            return true;
        }

        mapId = default;
        return false;
    }

    public IEnumerable<(MapId MapId, EntityUid Service)> GetServicesWithMaps()
    {
        foreach (var (mapId, service) in _servicesByMap)
        {
            if (EntityManager.EntityExists(service))
                yield return (mapId, service);
        }
    }
}
