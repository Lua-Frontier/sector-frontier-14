using Robust.Shared.GameStates;
using Content.Shared._Lua.Bank; // Lua

namespace Content.Shared._NF.Bank.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class BankAccountComponent : Component
{
	// The amount of money this entity has in their bank account.
	// Should not be modified directly, may be out-of-date.
	[DataField, Access(typeof(SharedBankSystem))]
	[AutoNetworkedField]
	public int Balance;

    // Lua start
    [ViewVariables, Access(typeof(SharedBankSystem))]
    public string YupiCode = string.Empty;

    [ViewVariables, Access(typeof(SharedBankSystem))]
    public List<BankAccountOperation> OperationHistory = new();
    // Lua end
}
