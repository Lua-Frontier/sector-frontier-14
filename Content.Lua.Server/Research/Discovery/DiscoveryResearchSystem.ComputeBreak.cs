using Content.Lua.Shared.Research.Discovery;
using Content.Shared.Damage;
using Robust.Server.GameObjects;
using Content.Shared.Research.Discovery;

namespace Content.Lua.Server.Research.Discovery;

public sealed partial class DiscoveryResearchSystem
{
    private readonly HashSet<EntityUid> _computeBreakCascade = new();

    private void OnComputeDamageChanged(EntityUid uid, ResearchComputeServerComponent compute, DamageChangedEvent args)
    {
        if (compute.Broken || !args.DamageIncreased)
            return;

        if (args.Damageable.TotalDamage < DiscoveryResearchConstants.ComputeExternalBreakDamage)
            return;

        BreakComputeFromExternal(uid, compute);
    }

    private void BreakComputeFromExternal(EntityUid uid, ResearchComputeServerComponent compute)
    {
        if (compute.Broken)
            return;

        var root = _computeBreakCascade.Count == 0;
        if (!_computeBreakCascade.Add(uid))
            return;

        try
        {
            SetComputeBroken(uid, compute);

            _explosion.QueueExplosion(
                uid,
                DiscoveryResearchConstants.ComputeBreakExplosionType,
                DiscoveryResearchConstants.ComputeBreakExplosionIntensity,
                DiscoveryResearchConstants.ComputeBreakExplosionSlope,
                DiscoveryResearchConstants.ComputeBreakExplosionMaxTile,
                canCreateVacuum: false,
                addLog: true);

            if (!TryComp(uid, out TransformComponent? xform))
                return;

            foreach (var other in _lookup.GetEntitiesInRange<ResearchComputeServerComponent>(
                         xform.Coordinates,
                         DiscoveryResearchConstants.ComputeBreakCascadeRange))
            {
                if (other.Owner == uid || other.Comp.Broken)
                    continue;

                BreakComputeFromExternal(other.Owner, other.Comp);
            }
        }
        finally
        {
            if (root)
                _computeBreakCascade.Clear();
        }
    }

    private void SetComputeBroken(EntityUid uid, ResearchComputeServerComponent compute)
    {
        if (compute.Broken)
            return;

        compute.Broken = true;
        compute.SparkAccumulator = 0f;
        Dirty(uid, compute);
        _powerReceiver.SetPowerDisabled(uid, true);
        UpdateComputeAppearance(uid, compute);
        Spawn(SparksEffectProto, Transform(uid).Coordinates);
        _audio.PlayPvs(ComputeDownSound, uid, ComputeDownAudioParams);
        RefreshComputeUi(uid, compute);
    }
}
