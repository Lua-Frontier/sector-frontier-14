// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.Achievements;

public sealed class AchievementUnlockRegistry
{
    private readonly Dictionary<string, List<string>> _bySpecies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _byCompany = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _byKillPrototype = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Prefix, string AchievementId)> _killPrefixes = new();
    private readonly List<(string AchievementId, string JobId)> _jobAvailable = new();
    private readonly List<(string AchievementId, string JobId, float Hours)> _jobPlayed = new();

    public IReadOnlyDictionary<string, List<string>> BySpecies => _bySpecies;
    public IReadOnlyDictionary<string, List<string>> ByCompany => _byCompany;
    public IReadOnlyDictionary<string, List<string>> ByKillPrototype => _byKillPrototype;
    public IReadOnlyList<(string AchievementId, string JobId)> JobAvailable => _jobAvailable;
    public IReadOnlyList<(string AchievementId, string JobId, float Hours)> JobPlayed => _jobPlayed;

    public void Build(IPrototypeManager prototypes)
    {
        _bySpecies.Clear();
        _byCompany.Clear();
        _byKillPrototype.Clear();
        _killPrefixes.Clear();
        _jobAvailable.Clear();
        _jobPlayed.Clear();

        foreach (var proto in prototypes.EnumeratePrototypes<AchievementPrototype>())
        {
            if (proto.Disabled)
                continue;

            if (proto.UnlockSpecies is { } species)
                Add(_bySpecies, species, proto.ID);

            if (proto.UnlockCompany is { } company)
                Add(_byCompany, company, proto.ID);

            foreach (var killId in proto.UnlockKillPrototypes)
                Add(_byKillPrototype, killId, proto.ID);

            if (proto.UnlockKillPrefix is { } prefix)
                _killPrefixes.Add((prefix, proto.ID));

            foreach (var extraPrefix in proto.UnlockKillPrefixes)
                _killPrefixes.Add((extraPrefix, proto.ID));

            if (proto.UnlockJobAvailable is { } available)
                _jobAvailable.Add((proto.ID, available));

            if (proto.UnlockJobPlayed is { } played)
                _jobPlayed.Add((proto.ID, played, proto.UnlockJobPlayedHours));
        }

        _killPrefixes.Sort(static (a, b) => b.Prefix.Length.CompareTo(a.Prefix.Length));
    }

    public IEnumerable<string> MatchKill(string? prototypeId)
    {
        if (string.IsNullOrEmpty(prototypeId))
            yield break;

        if (_byKillPrototype.TryGetValue(prototypeId, out var exact))
        {
            foreach (var id in exact)
                yield return id;
        }

        foreach (var (prefix, achievementId) in _killPrefixes)
        {
            if (prototypeId.StartsWith(prefix, StringComparison.Ordinal))
            {
                yield return achievementId;
                break;
            }
        }
    }

    private static void Add(Dictionary<string, List<string>> map, string key, string achievementId)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = new List<string>();
            map[key] = list;
        }

        if (!list.Contains(achievementId))
            list.Add(achievementId);
    }
}
