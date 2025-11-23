using Content.Shared._Mono.Company;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;

namespace Content.Shared._Lua.Company;

/// <summary>
/// Это реализация поддержки фракций для ролей, необходимо будет доработать роли под поддержку фракций Nanotrasen, Syndicate, Pirate и других.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class CompanyRequirement : JobRequirement
{
    [DataField(required: true)]
    public HashSet<string> Companies = new();

    public override bool Check(
        IEntityManager entManager,
        IPrototypeManager protoManager,
        HumanoidCharacterProfile? profile,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = FormattedMessage.Empty;
        if (profile == null) return true;
        var profileCompany = profile.Company ?? "None";
        var matches = false;
        foreach (var company in Companies)
        {
            if (string.Equals(company, profileCompany, StringComparison.OrdinalIgnoreCase))
            {
                matches = true;
                break;
            }
        }

        if (!Inverted && matches || Inverted && !matches) return true;
        var companyNames = new List<string>(Companies.Count);
        foreach (var companyId in Companies)
        {
            if (protoManager.TryIndex<CompanyPrototype>(companyId, out var companyProto)) companyNames.Add(companyProto.Name);
            else companyNames.Add(companyId);
        }
        var companyList = string.Join(", ", companyNames);
        var locKey = Inverted ? "role-timer-company-blacklisted" : "role-timer-company-whitelisted";
        reason = FormattedMessage.FromMarkupPermissive(Loc.GetString(locKey, ("companies", companyList)));
        return false;
    }
}

