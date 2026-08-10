using Content.Server.Gatherable.Components;
using Content.Shared.Mining.Components;
using Content.Shared.Projectiles;
using Content.Shared.Whitelist;

namespace Content.Server.Gatherable;

public sealed partial class GatherableSystem
{
    private void InitializeProjectile()
    {
        SubscribeLocalEvent<GatheringProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnProjectileHit(Entity<GatheringProjectileComponent> gathering, ref ProjectileHitEvent args)
    {
        if (!TryComp<ProjectileComponent>(gathering, out _) ||
            gathering.Comp.Amount <= 0 ||
            !TryComp<GatherableComponent>(args.Target, out var gatherable))
        {
            return;
        }

        if (TryComp<OreVeinComponent>(args.Target, out var oreVein)
            && _whitelistSystem.IsWhitelistPass(oreVein.GatherDestructionWhitelist, gathering.Owner))
        {
            oreVein.PreventSpawning = true;
        }

        if (gatherable.Gathered)
        {
            args.Handled = true;
            return;
        }

        Gather(args.Target, gathering, gatherable);
        gathering.Comp.Amount--;
        args.Handled = true;
    }
}
