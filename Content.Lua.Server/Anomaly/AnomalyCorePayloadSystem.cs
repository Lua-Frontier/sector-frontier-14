using Content.Server.Administration.Logs;
using Content.Server.Explosion.EntitySystems;
using Content.Lua.Shared.Anomaly;
using Content.Shared.Anomaly.Components;
using Content.Shared.Database;
using Content.Shared.Payload.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Lua.Server.Anomaly;

public sealed class AnomalyCorePayloadSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    private static readonly HashSet<string> SupercriticalEffectNames = new(StringComparer.Ordinal)
    {
        "ExplosionAnomaly",
        "ElectricityAnomaly",
        "GravityAnomaly",
        "PyroclasticAnomaly",
        "BluespaceAnomaly",
        "TechAnomaly",
        "GasProducerAnomaly",
        "PuddleCreateAnomaly",
        "InjectionAnomaly",
        "ProjectileAnomaly",
        "EntitySpawnAnomaly",
        "TileSpawnAnomaly",
        "SolutionContainerManager",
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AnomalyCorePayloadComponent, TriggerEvent>(OnCoreTriggered);
    }

    private void OnCoreTriggered(Entity<AnomalyCorePayloadComponent> core, ref TriggerEvent args)
    {
        if (!_prototypes.TryIndex(core.Comp.Anomaly, out EntityPrototype? anomalyProto))
        {
            Log.Warning($"Anomaly core payload {ToPrettyString(core)} references missing anomaly prototype {core.Comp.Anomaly}.");
            return;
        }

        var detonateAt = core.Owner;
        EntityUid? grenade = null;
        if (_container.TryGetContainingContainer((core.Owner, null, null), out var container) &&
            HasComp<PayloadCaseComponent>(container.Owner))
        {
            grenade = container.Owner;
            detonateAt = container.Owner;
        }

        _transform.AttachToGridOrMap(detonateAt);
        ApplySupercriticalEffects(detonateAt, anomalyProto);

        if (anomalyProto.TryGetComponent(out AnomalyComponent? anomaly, _factory))
        {
            var sound = anomaly.SupercriticalSoundAtAnimationStart ?? anomaly.SupercriticalSound;
            if (sound != null)
                _audio.PlayPvs(sound, detonateAt);
        }

        var ev = new AnomalySupercriticalEvent(detonateAt, PowerModifier: 1f);
        RaiseLocalEvent(detonateAt, ref ev, broadcast: true);

        _adminLogger.Add(LogType.Explosion,
            LogImpact.High,
            $"Anomaly core payload {ToPrettyString(core)} detonated supercritical effects from {core.Comp.Anomaly} at {_transform.GetMapCoordinates(detonateAt)}");

        if (grenade is { } caseUid && !Deleted(caseUid))
            QueueDel(caseUid);

        if (!Deleted(core.Owner))
            QueueDel(core.Owner);

        args.Handled = true;
    }

    private void ApplySupercriticalEffects(EntityUid uid, EntityPrototype anomalyProto)
    {
        foreach (var (name, entry) in anomalyProto.Components)
        {
            if (!SupercriticalEffectNames.Contains(name))
                continue;

            if (!_factory.TryGetRegistration(name, out var registration))
                continue;

            if (HasComp(uid, registration.Type))
                RemComp(uid, registration.Type);

            var component = (Component)_factory.GetComponent(registration.Type);
            var temp = (object)component;
            _serialization.CopyTo(entry.Component, ref temp);
            EntityManager.AddComponent(uid, (Component)temp!, overwrite: true);
        }
    }
}
