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
    /// <summary>
    /// Localized summary string for UI: "Итог снижения цен".
    /// </summary>
    public string? TotalReductionText;

    //Lua: новое поле - реальная стоимость (после налогов/динамики)
    public int Real;

    //Lua: выгода продажи (реальная - оценочная), но не ниже 0
    //Lua: для клиента - это проценты по динамике (0..1), я показываю их в UI при необходимости
    public int ReductionPercent;

    /// <summary>
    /// When true, client should show a minimal UI (for pirate/freelancer consoles):
    /// only appraisal, count and buttons.
    /// </summary>
    public bool MinimalUi;

    //Lua: заменён primary-constructor на явный конструктор для совместимости
    public NFCargoPalletConsoleInterfaceState(int appraisal, int count, bool enabled, string? totalReductionText = null, int real = 0, int reductionPercent = 0, bool minimalUi = false)
    {
        Appraisal = appraisal; //Lua
        Count = count; //Lua
        Enabled = enabled; //Lua
        TotalReductionText = totalReductionText; //Lua
        Real = real; //Lua
        ReductionPercent = reductionPercent; //Lua
        MinimalUi = minimalUi;
    }
    // Lua end
}
