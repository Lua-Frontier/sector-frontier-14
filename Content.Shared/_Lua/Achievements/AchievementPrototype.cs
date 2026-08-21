// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Linq;
using System.Numerics;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Lua.Achievements;

[Prototype]
public sealed partial class AchievementPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name = string.Empty;

    [DataField(required: true)]
    public LocId Description = string.Empty;

    [DataField]
    public SpriteSpecifier? Icon;

    [DataField]
    public List<SpriteSpecifier> IconLayers = new();

    [DataField]
    public LocId? Category;

    [DataField]
    public List<ProtoId<AchievementPrototype>> Prerequisites = new();

    [DataField]
    public Vector2i? Position;

    [DataField]
    public bool Hidden;

    [DataField]
    public bool Disabled;

    [DataField]
    public string? UnlockSpecies;

    [DataField]
    public string? UnlockCompany;

    [DataField]
    public List<string> UnlockKillPrototypes = new();

    [DataField]
    public string? UnlockKillPrefix;

    [DataField]
    public List<string> UnlockKillPrefixes = new();

    [DataField]
    public int UnlockKillCount;

    [DataField]
    public ProtoId<JobPrototype>? UnlockJobAvailable;

    [DataField]
    public ProtoId<JobPrototype>? UnlockJobPlayed;

    [DataField]
    public float UnlockJobPlayedHours = 1f;

    [DataField]
    public List<AchievementItemReward> Rewards = new();

    public bool HasRewards => Rewards.Count > 0;

    public IReadOnlyList<SpriteSpecifier> ResolveIconLayers()
    {
        if (IconLayers.Count == 0)
        {
            if (Icon != null && Icon != SpriteSpecifier.Invalid)
                return new[] { Icon };

            return Array.Empty<SpriteSpecifier>();
        }

        var layers = new List<SpriteSpecifier>(IconLayers.Count + 1);
        if (Icon != null && Icon != SpriteSpecifier.Invalid)
            layers.Add(Icon);

        layers.AddRange(IconLayers);
        return layers;
    }

    public bool IsKillAchievement =>
        UnlockKillPrefix != null || UnlockKillPrefixes.Count > 0 || UnlockKillPrototypes.Count > 0;

    public int RequiredKillCount => UnlockKillCount > 0 ? UnlockKillCount : 1;
}
