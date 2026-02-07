using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Shared._Lua.SponsorLoadout;

[Prototype("sponsorLoadout")]
public sealed partial class SponsorLoadoutPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("entity", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string EntityId { get; private set; } = default!;

    [DataField("sponsorOnly")]
    public bool SponsorOnly = false;

    [DataField("whitelistJobs", customTypeSerializer: typeof(PrototypeIdListSerializer<JobPrototype>))]
    public List<string>? WhitelistJobs { get; private set; }

    [DataField("blacklistJobs", customTypeSerializer: typeof(PrototypeIdListSerializer<JobPrototype>))]
    public List<string>? BlacklistJobs { get; private set; }

    [DataField("speciesRestriction")]
    public List<string>? SpeciesRestrictions { get; private set; }

    [DataField]
    public string? Login { get; private set; }

    [DataField("sponsorRole")]
    public string? SponsorRole { get; private set; }
}
