namespace Content.Server._Mono.Company;

public sealed class CompanySetEvent : EntityEventArgs
{
	public string OldCompanyId { get; }
	public string NewCompanyId { get; }
	public bool Changed { get; }

	public CompanySetEvent(string oldCompanyId, string newCompanyId, bool changed)
	{
		OldCompanyId = oldCompanyId;
		NewCompanyId = newCompanyId;
		Changed = changed;
	}
}