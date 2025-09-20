using Robust.Shared.GameStates;

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

	//Lua Start The UPI code is linked via the BankAccount component
	[DataField, Access(typeof(SharedBankSystem))]
	[AutoNetworkedField]
	public string YupiCode = string.Empty;
	//Lua End
}
