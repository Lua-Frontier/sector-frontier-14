// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Diagnostics.CodeAnalysis;
using Content.Shared.Localizations;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Lua.Achievements;

public static class AchievementJobText
{
    public static string GetName(AchievementPrototype proto, IPrototypeManager prototypes)
    {
        if (TryGetJob(proto, prototypes, out var job))
        {
            if (proto.UnlockJobAvailable != null)
                return Loc.GetString("achievement-job-unlock-name", ("job", job.LocalizedName));

            if (proto.UnlockJobPlayed != null)
                return Loc.GetString("achievement-job-play-name", ("job", job.LocalizedName));
        }

        return Loc.GetString(proto.Name);
    }

    public static string GetDescription(
        AchievementPrototype proto,
        IEntityManager entities,
        IPrototypeManager prototypes)
    {
        if (!TryGetJob(proto, prototypes, out var job))
            return Loc.GetString(proto.Description);

        if (proto.UnlockJobAvailable != null)
        {
            return Loc.GetString(
                "achievement-job-unlock-desc",
                ("job", job.LocalizedName),
                ("time", FormatJobPlaytimeRequirements(job, entities, prototypes)));
        }

        if (proto.UnlockJobPlayed != null)
        {
            var time = ContentLocalizationManager.FormatPlaytime(TimeSpan.FromHours(proto.UnlockJobPlayedHours));
            return Loc.GetString(
                "achievement-job-play-desc",
                ("job", job.LocalizedName),
                ("time", time));
        }

        return Loc.GetString(proto.Description);
    }

    public static SpriteSpecifier GetIcon(AchievementPrototype proto, IPrototypeManager prototypes)
    {
        var layers = GetIconLayers(proto, prototypes);
        return layers.Count > 0 ? layers[0] : SpriteSpecifier.Invalid;
    }

    public static IReadOnlyList<SpriteSpecifier> GetIconLayers(AchievementPrototype proto, IPrototypeManager prototypes)
    {
        if (TryGetJob(proto, prototypes, out var job) &&
            prototypes.TryIndex(job.Icon, out JobIconPrototype? jobIcon))
            return new[] { jobIcon.Icon };

        return proto.ResolveIconLayers();
    }

    public static bool IsPlaytimeRequirement(JobRequirement req)
    {
        return req is OverallPlaytimeRequirement or RoleTimeRequirement or DepartmentTimeRequirement;
    }

    public static bool JobPlaytimeRequirementsMet(
        JobPrototype job,
        IEntityManager entities,
        IPrototypeManager prototypes,
        IReadOnlyDictionary<string, TimeSpan> playTimes)
    {
        var requirements = entities.System<SharedRoleSystem>().GetJobRequirement(job);
        if (requirements == null)
            return true;

        foreach (var req in requirements)
        {
            if (!IsPlaytimeRequirement(req))
                continue;

            if (!req.Check(entities, prototypes, null, playTimes, out _))
                return false;
        }

        return true;
    }

    private static string FormatJobPlaytimeRequirements(
        JobPrototype job,
        IEntityManager entities,
        IPrototypeManager prototypes)
    {
        var requirements = entities.System<SharedRoleSystem>().GetJobRequirement(job);
        if (requirements == null)
            return ContentLocalizationManager.FormatPlaytime(TimeSpan.Zero);

        var parts = new List<string>();
        OverallPlaytimeRequirement? onlyOverall = null;
        var playtimeCount = 0;

        foreach (var req in requirements)
        {
            if (req.Inverted || !IsPlaytimeRequirement(req))
                continue;

            playtimeCount++;
            if (req is OverallPlaytimeRequirement overall)
                onlyOverall = overall;

            var part = FormatRequirement(req, prototypes);
            if (part != null)
                parts.Add(part);
        }

        if (playtimeCount == 1 && onlyOverall != null)
            return ContentLocalizationManager.FormatPlaytime(onlyOverall.Time);

        return parts.Count == 0
            ? ContentLocalizationManager.FormatPlaytime(TimeSpan.Zero)
            : string.Join(", ", parts);
    }

    private static string? FormatRequirement(JobRequirement req, IPrototypeManager prototypes)
    {
        switch (req)
        {
            case OverallPlaytimeRequirement overall:
                return Loc.GetString(
                    "achievement-job-req-overall",
                    ("time", ContentLocalizationManager.FormatPlaytime(overall.Time)));
            case RoleTimeRequirement role:
                var roleName = role.Role.ToString();
                foreach (var other in prototypes.EnumeratePrototypes<JobPrototype>())
                {
                    if (other.PlayTimeTracker == role.Role)
                    {
                        roleName = other.LocalizedName;
                        break;
                    }
                }

                return Loc.GetString(
                    "achievement-job-req-role",
                    ("time", ContentLocalizationManager.FormatPlaytime(role.Time)),
                    ("job", roleName));
            case DepartmentTimeRequirement dept:
                var deptName = dept.Department.ToString();
                if (prototypes.TryIndex(dept.Department, out DepartmentPrototype? department))
                    deptName = Loc.GetString(department.Name);

                return Loc.GetString(
                    "achievement-job-req-department",
                    ("time", ContentLocalizationManager.FormatPlaytime(dept.Time)),
                    ("department", deptName));
            default:
                return null;
        }
    }

    private static bool TryGetJob(
        AchievementPrototype proto,
        IPrototypeManager prototypes,
        [NotNullWhen(true)] out JobPrototype? job)
    {
        var jobId = proto.UnlockJobAvailable ?? proto.UnlockJobPlayed;
        if (jobId is { } id)
            return prototypes.TryIndex(id, out job);

        job = null;
        return false;
    }
}
