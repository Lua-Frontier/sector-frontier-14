namespace Content.Lua.Shared.AutoSalary;

[RegisterComponent]
public sealed partial class SalaryTrackingComponent : Component
{
    [ViewVariables]
    [DataField]
    public EntityUid Station;

    [ViewVariables]
    [DataField]
    public string JobId = string.Empty;
}
