using Content.Server._Lua.MedicalPod.Components;
using Content.Server.Storage.Components;
using Content.Shared.Atmos;
using Robust.Shared.Timing;

namespace Content.Server._Lua.MedicalPod.Systems;

public sealed class MedicalPodAirRegeneratorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MedicalPodAirRegeneratorComponent, EntityStorageComponent>();
        while (query.MoveNext(out var uid, out var regen, out var storage))
        {
            if (storage.Open) continue;
            if (regen.UpdateInterval > 0f)
            {
                if (_timing.CurTime < regen.NextUpdate) continue;
                regen.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(regen.UpdateInterval);
            }
            var air = storage.Air;
            var changed = false;
            changed |= EnsureAtLeast(air, Gas.Oxygen, regen.TargetOxygenMoles);
            changed |= EnsureAtLeast(air, Gas.Nitrogen, regen.TargetNitrogenMoles);
            changed |= EnsureAtLeast(air, Gas.WaterVapor, regen.TargetWaterVaporMoles);
            changed |= EnsureAtMost(air, Gas.CarbonDioxide, regen.MaxCarbonDioxideMoles);
            changed |= EnsureAtMost(air, Gas.Plasma, 0f);
            changed |= EnsureAtMost(air, Gas.Ammonia, 0f);
            changed |= EnsureAtMost(air, Gas.NitrousOxide, 0f);
            changed |= EnsureAtMost(air, Gas.Tritium, 0f);
            changed |= EnsureAtMost(air, Gas.Frezon, 0f);
            if (changed) Dirty(uid, storage);
        }
    }

    private static bool EnsureAtLeast(GasMixture air, Gas gas, float moles)
    {
        var current = air.GetMoles(gas);
        if (current >= moles) return false;
        air.SetMoles(gas, moles);
        return true;
    }

    private static bool EnsureAtMost(GasMixture air, Gas gas, float moles)
    {
        var current = air.GetMoles(gas);
        if (current <= moles) return false;
        air.SetMoles(gas, moles);
        return true;
    }
}
