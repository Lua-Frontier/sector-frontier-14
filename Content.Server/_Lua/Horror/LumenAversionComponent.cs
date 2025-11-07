using Content.Shared.Damage;

namespace Content.Server._Lua.Horror;

[RegisterComponent]
public sealed partial class LumenAversionComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("lightDamage")]
    public DamageSpecifier LightDamage = new()
    {
        DamageDict = new()
        { { "Heat", 15 } }
    };

    [ViewVariables(VVAccess.ReadWrite), DataField("enabled")]
    public bool Enabled = true;

    [DataField("darkRegen")]
    public DamageSpecifier DarkRegen = new()
    {
        DamageDict = new()
        {
            { "Blunt", -15 },
            { "Slash", -15 },
            { "Piercing", -15 },
            { "Heat", -15 },
            { "Shock", -15 }
        }
    };
}


