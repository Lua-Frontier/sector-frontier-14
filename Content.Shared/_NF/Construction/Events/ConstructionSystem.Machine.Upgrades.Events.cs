using Content.Shared.Stacks;
using Robust.Shared.Utility;
using Content.Shared.FixedPoint; // Lua

namespace Content.Shared.Construction.Components;

public struct MachinePartState
{
    public MachinePartComponent Part;
    public StackComponent? Stack;
    // If item is a stack, return the count in the stack, otherwise it's a singular, non-stackable part
    public int Quantity()
    {
        return Stack?.Count ?? 1;
    }
}

public sealed class RefreshPartsEvent : EntityEventArgs
{
    public IReadOnlyList<MachinePartState> Parts = new List<MachinePartState>(); // Frontier: MachinePartComponent<MachinePartState

    public Dictionary<string, float> PartRatings = new Dictionary<string, float>();
}

public sealed class UpgradeExamineEvent : EntityEventArgs
{
    private FormattedMessage _message;

    public UpgradeExamineEvent(ref FormattedMessage message)
    {
        _message = message;
    }

    /// <summary>
    /// Add a line to the upgrade examine tooltip with a percentage-based increase or decrease.
    /// </summary>
    public void AddPercentageUpgrade(string upgradedLocId, float multiplier)
    {
        var percent = Math.Round(100 * MathF.Abs(multiplier - 1), 2);
        var locId = multiplier switch
        {
            < 1 => "machine-upgrade-decreased-by-percentage",
            1 or float.NaN => "machine-upgrade-not-upgraded",
            > 1 => "machine-upgrade-increased-by-percentage",
        };
        var upgraded = Loc.GetString(upgradedLocId);
        this._message.TryAddMarkup(Loc.GetString(locId, ("upgraded", upgraded), ("percent", percent)) + '\n', out _); // Frontier: AddMarkup<TryAddMarkup
    }

    // Lua start
    public void AddPercentageUpgrade(string upgradedLocId, float multiplier, float timeModifier)
    {
        // Lube's timeModifier is 0.5
        // Glue's timeModifier is 5
        var absolute = 100 * multiplier / timeModifier;

        var locId = absolute switch
        {
            < 100 => "machine-upgrade-decreased-by-percentage-extra",
            100 or float.NaN => "machine-upgrade-not-upgraded",
            > 100 => "machine-upgrade-increased-by-percentage-extra"
        };

        var change = absolute switch
        {
            < 100 => 100 - absolute,
            100 or float.NaN => 100,
            > 100 => absolute - 100
        };

        var locColor = timeModifier switch
        {
            < 1 => "#9AFFC1", // Boosted
            1 or float.NaN => "#FFFFFF",
            > 1 => "#FFA2A2" // Slowed
        };

        var upgraded = Loc.GetString(upgradedLocId);
        _message.TryAddMarkup(Loc.GetString(locId, ("upgraded", upgraded), ("percent", Math.Round(change, 2)), ("color", locColor)) + '\n', out _);
    }
    // Lua end

    /// <summary>
    /// Add a line to the upgrade examine tooltip with a numeric increase or decrease.
    /// </summary>
    public void AddNumberUpgrade(string upgradedLocId, int number)
    {
        var difference = Math.Abs(number);
        var locId = number switch
        {
            < 0 => "machine-upgrade-decreased-by-amount",
            0 => "machine-upgrade-not-upgraded",
            > 0 => "machine-upgrade-increased-by-amount",
        };
        var upgraded = Loc.GetString(upgradedLocId);
        this._message.TryAddMarkup(Loc.GetString(locId, ("upgraded", upgraded), ("difference", difference)) + '\n', out _); // Frontier: AddMarkup<TryAddMarkup
    }
}
