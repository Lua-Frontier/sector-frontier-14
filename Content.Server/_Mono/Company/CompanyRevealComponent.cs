using Robust.Shared.GameObjects;

namespace Content.Server._Mono.Company;

[RegisterComponent]
public sealed partial class CompanyRevealComponent : Component
{
    public HashSet<string> RevealedToPlayerIds = new(StringComparer.Ordinal);
}