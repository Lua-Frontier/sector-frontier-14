using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._Goobstation.SpaceWhale;

[RegisterComponent, NetworkedComponent]
public sealed partial class DamageOnCollideComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    [DataField]
    public EntityWhitelist? IgnoreWhitelist = new();

    [DataField]
    public EntityWhitelist? Whitelist = new();

    [DataField]
    public bool Inverted = false;
}
