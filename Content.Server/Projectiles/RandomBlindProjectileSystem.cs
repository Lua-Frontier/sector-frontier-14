using Content.Server.Chat.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Projectiles;
using Content.Shared.StatusEffect;
using Robust.Shared.Random;

namespace Content.Server.Projectiles;

/// <summary>
/// Frontier: handles RandomBlindChance on projectiles via ProjectileHitEvent.
/// Separate from SharedProjectileSystem to avoid duplicate event subscriptions.
/// </summary>
public sealed class RandomBlindProjectileSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _statusEffectsSystem = default!;
    [Dependency] private readonly BlindableSystem _blindingSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnProjectileHit(EntityUid uid, ProjectileComponent comp, ref ProjectileHitEvent args)
    {
        if (comp.RandomBlindChance <= 0.0f || !_random.Prob(comp.RandomBlindChance))
            return;

        TryBlind(args.Target);
    }

    private void TryBlind(EntityUid target)
    {
        if (!TryComp<BlindableComponent>(target, out var blindable) || blindable.IsBlind)
            return;

        var eyeProtectionEv = new GetEyeProtectionEvent();
        RaiseLocalEvent(target, eyeProtectionEv);

        var time = (float)(TimeSpan.FromSeconds(2) - eyeProtectionEv.Protection).TotalSeconds;
        if (time <= 0)
            return;

        _chat.TryEmoteWithoutChat(target, "Scream");

        _blindingSystem.AdjustEyeDamage((target, blindable), 1);
        var statusTimeSpan = TimeSpan.FromSeconds(time * MathF.Sqrt(blindable.EyeDamage));
        _statusEffectsSystem.TryAddStatusEffect(target, TemporaryBlindnessSystem.BlindingStatusEffect,
            statusTimeSpan, false, TemporaryBlindnessSystem.BlindingStatusEffect);
    }
}
