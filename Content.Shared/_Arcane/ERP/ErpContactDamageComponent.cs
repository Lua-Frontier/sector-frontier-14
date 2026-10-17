using Content.Shared.Damage;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.ERP;

[RegisterComponent]
public sealed partial class ErpContactDamageComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    [DataField]
    public ProtoId<LocalizedDatasetPrototype>? Popups;
}
