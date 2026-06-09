using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared.Damage.Components;

[NetworkedComponent, RegisterComponent, AutoGenerateComponentState]
public sealed partial class DamageOnHoldingAliveOnlyComponent : Component
{
    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new();
    
    [DataField, AutoNetworkedField]
    public float Interval = 1.0f;

    [DataField, AutoNetworkedField]
    public bool IgnoreResistances = false;
    
    public TimeSpan NextDamageTime;
}