using Robust.Shared.Timing;
using Content.Server.Administration.Systems;
using Content.Shared.Alert;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Prototypes;

namespace Content.Server._NF.PacifiedZone;

public sealed class PacifiedZoneGeneratorSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly SharedJobSystem _jobSystem = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly AdminSystem _admin = default!;

    private static readonly ProtoId<AlertPrototype> AlertProto = "PacifiedZone";
    private readonly HashSet<EntityUid> _trackedNewBuffer = new();
    private readonly List<EntityUid> _trackedRemoveBuffer = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PacifiedZoneGeneratorComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<PacifiedZoneGeneratorComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnComponentInit(EntityUid uid, PacifiedZoneGeneratorComponent component, ComponentInit args)
    {
        UpdatePacifiedState(uid, component);
    }

    private void OnComponentShutdown(EntityUid uid, PacifiedZoneGeneratorComponent component, ComponentShutdown args)
    {
        foreach (var entity in component.TrackedEntities)
        {
            RemComp<PacifiedComponent>(entity);
            RemComp<PacifiedByZoneComponent>(entity);
            DisableAlert(entity);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var genQuery = AllEntityQuery<PacifiedZoneGeneratorComponent>();
        while (genQuery.MoveNext(out var genUid, out var component))
        {
            if (_gameTiming.CurTime < component.NextUpdate)
                continue;

            UpdatePacifiedState(genUid, component);
        }
    }

    private void UpdatePacifiedState(EntityUid genUid, PacifiedZoneGeneratorComponent component)
    {
        _trackedNewBuffer.Clear();
        var query = _lookup.GetEntitiesInRange<HumanoidAppearanceComponent>(Transform(genUid).Coordinates, component.Radius);
        foreach (var humanoidUid in query)
        {
            if (!_mindSystem.TryGetMind(humanoidUid, out var mindId, out var mind))
                continue;

            _jobSystem.MindTryGetJobId(mindId, out var jobId);

            if (jobId != null && component.ImmuneRoles.Contains(jobId.Value))
                continue;

            if (component.ImmunePlaytime != null)
            {
                var playerInfo = _admin.GetCachedPlayerInfo(mind?.UserId);
                if (playerInfo != null && playerInfo.OverallPlaytime >= component.ImmunePlaytime)
                {
                    continue;
                }
            }

            if (!component.TrackedEntities.Contains(humanoidUid))
            {
                if (HasComp<PacifiedComponent>(humanoidUid))
                    continue;

                var pacifiedComponent = AddComp<PacifiedComponent>(humanoidUid);
                EnableAlert(humanoidUid, pacifiedComponent);
                AddComp<PacifiedByZoneComponent>(humanoidUid);
            }

            _trackedNewBuffer.Add(humanoidUid);
        }

        _trackedRemoveBuffer.Clear();
        foreach (var humanoidUid in component.TrackedEntities)
        {
            if (!_trackedNewBuffer.Contains(humanoidUid))
                _trackedRemoveBuffer.Add(humanoidUid);
        }

        foreach (var humanoidUid in _trackedRemoveBuffer)
        {
            RemComp<PacifiedComponent>(humanoidUid);
            RemComp<PacifiedByZoneComponent>(humanoidUid);
            DisableAlert(humanoidUid);
        }

        component.TrackedEntities.Clear();
        component.TrackedEntities.UnionWith(_trackedNewBuffer);
        component.NextUpdate = _gameTiming.CurTime + component.UpdateInterval;
    }

    private void EnableAlert(EntityUid entity, PacifiedComponent pacified)
    {
        _alerts.ClearAlert(entity, pacified.PacifiedAlert);
        _alerts.ShowAlert(entity, AlertProto);
    }

    private void DisableAlert(EntityUid entity)
    {
        _alerts.ClearAlert(entity, AlertProto);
    }
}
