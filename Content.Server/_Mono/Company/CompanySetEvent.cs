using Robust.Shared.GameObjects;

namespace Content.Server._Mono.Company;

public sealed class CompanySetEvent : EntityEventArgs
{
    public EntityUid Entity { get; }
    public string OldCompanyId { get; }
    public string NewCompanyId { get; }
    public bool Changed { get; }

    public CompanySetEvent(EntityUid entity, string oldCompanyId, string newCompanyId, bool changed)
    {
        Entity = entity;
        OldCompanyId = oldCompanyId;
        NewCompanyId = newCompanyId;
        Changed = changed;
    }
}
