using Robust.Shared.Serialization;

namespace Content.Shared._NF.Cargo.BUI;

[NetSerializable, Serializable]
public sealed class NFCargoPalletConsoleInterfaceState : BoundUserInterfaceState // Lua delete: ( int appraisal, int count, bool enabled)
{
    /// <summary>
    /// The estimated apraised value of all the entities on top of pallets on the same grid as the console.
    /// </summary>
    public int Appraisal; // Lua delete: = appraisal

    /// <summary>
    /// The number of entities on top of pallets on the same grid as the console.
    /// </summary>
    public int Count; // Lua delete: = count

    /// <summary>
    /// True if the buttons should be enabled.
    /// </summary>
    public bool Enabled; // Lua delete: = enabled
    // Lua start
    public string? TotalReductionText;
    public int Real;
    public int ReductionPercent;
    public bool MinimalUi;
    public List<PalletTaxEntry> TaxEntries = new(); // Lua
    public int BaseAmount;
    public int ConsoleDeltaAmount;
    public int DynamicDeltaAmount;
    public NFCargoPalletConsoleInterfaceState(int appraisal, int count, bool enabled, string? totalReductionText = null, int real = 0, int reductionPercent = 0, bool minimalUi = false, List<PalletTaxEntry>? taxEntries = null, int baseAmount = 0, int consoleDeltaAmount = 0, int dynamicDeltaAmount = 0)
    {
        Appraisal = appraisal;
        Count = count;
        Enabled = enabled;
        TotalReductionText = totalReductionText;
        Real = real;
        ReductionPercent = reductionPercent;
        MinimalUi = minimalUi;
        if (taxEntries != null) TaxEntries = taxEntries;
        BaseAmount = baseAmount;
        ConsoleDeltaAmount = consoleDeltaAmount;
        DynamicDeltaAmount = dynamicDeltaAmount;
    }
    // Lua end
}

[NetSerializable, Serializable]
public sealed class PalletTaxEntry
{
    public string Name = string.Empty;
    public int Percent;

    public PalletTaxEntry()
    {
    }

    public PalletTaxEntry(string name, int percent)
    {
        Name = name;
        Percent = percent;
    }
}
