using Content.Shared._NF.Bank.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Mono.ItemTax.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ItemTaxComponent : Component
{
    [DataField]
    public Dictionary<SectorBankAccount, float> TaxAccounts = new();
}
