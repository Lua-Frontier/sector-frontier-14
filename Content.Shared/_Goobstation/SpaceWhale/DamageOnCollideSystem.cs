using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Robust.Shared.Physics.Events;

namespace Content.Shared._Goobstation.SpaceWhale;

public sealed class DamageOnCollideSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageOnCollideComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<DamageOnCollideComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnStartCollide(EntityUid uid, DamageOnCollideComponent component, ref StartCollideEvent args)
    {
        if (component.Whitelist != null && !_whitelist.IsWhitelistPass(component.Whitelist, args.OtherEntity)) return;
        var target = component.Inverted ? args.OtherEntity : uid;
        _damageable.TryChangeDamage(target, component.Damage);
    }

    private void OnPreventCollide(EntityUid uid, DamageOnCollideComponent component, ref PreventCollideEvent args)
    {
        if (component.Whitelist != null && _whitelist.IsWhitelistPass(component.Whitelist, args.OtherEntity)) return;
        if (component.IgnoreWhitelist != null && _whitelist.IsWhitelistPass(component.IgnoreWhitelist, args.OtherEntity)) args.Cancelled = true;
    }
}
