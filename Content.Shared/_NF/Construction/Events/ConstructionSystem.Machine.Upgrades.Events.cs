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

    /// <summary>
    /// Add a line to the upgrade examine tooltip with a percentage-based increase or decrease
    /// Includes time modifier, bases color on it and affects total percentage.
    /// </summary>
    public void AddPercentageUpgrade(string upgradedLocId, float multiplier, float timeModifier)
    {
        var locId = multiplier switch
        {
            < 1 => "machine-upgrade-decreased-by-percentage-extra",
            1 or float.NaN => "machine-upgrade-not-upgraded-extra",
            > 1 => "machine-upgrade-increased-by-percentage-extra",
        };

        // Lube's timeModifier is 0.5
        // Glue's timeModifier is 5
        // If percent is 300% increase, lube should make it 600%.
        // And if percent is 66% decrease, lube should make it 33%.
        FixedPoint2 percent = multiplier switch
        {
            < 1 => 100 * timeModifier * MathF.Abs(multiplier - 1),
            1 or float.NaN => 100 / timeModifier,
            > 1 => 100 / timeModifier * MathF.Abs(multiplier - 1)
        };

        var locColor = timeModifier switch
        {
            < 1 => "#6DFFA5", // Boosted
            1 or float.NaN => "#FFFFFF",
            > 1 => "#FF7A7A" // Slowed
        };

        var upgraded = Loc.GetString(upgradedLocId);
        this._message.TryAddMarkup(Loc.GetString(locId, ("upgraded", upgraded), ("percent", percent), ("color", locColor)) + '\n', out _);
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