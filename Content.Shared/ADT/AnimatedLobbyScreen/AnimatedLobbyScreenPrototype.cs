using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared.ADT;

/// <summary>
/// This is a prototype for...
/// </summary>
[Prototype]
public sealed partial class AnimatedLobbyScreenPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Path = default!;

    [DataField]
    public byte BeginMonth;

    [DataField]
    public byte BeginDay;

    [DataField]
    public byte EndMonth;

    [DataField]
    public byte EndDay;

    public bool HasDateRestriction => BeginMonth != 0;

    public bool IsAvailable(DateTime date)
    {
        if (!HasDateRestriction)
            return true;

        var beginMonth = BeginMonth;
        var beginDay = BeginDay == 0 ? (byte) 1 : BeginDay;
        var endMonth = EndMonth == 0 ? BeginMonth : EndMonth;
        var endDay = EndDay == 0 ? beginDay : EndDay;

        if (endMonth > beginMonth)
        {
            if (date.Month == endMonth && date.Day <= endDay)
                return true;
            if (date.Month == beginMonth && date.Day >= beginDay)
                return true;
            if (date.Month > beginMonth && date.Month < endMonth)
                return true;
        }
        else if (endMonth == beginMonth)
        {
            if (date.Month == beginMonth && date.Day >= beginDay && date.Day <= endDay)
                return true;
        }
        else
        {
            if (date.Month >= beginMonth && date.Day >= beginDay)
                return true;
            if (date.Month <= endMonth && date.Day <= endDay)
                return true;
        }

        return false;
    }

    public static List<AnimatedLobbyScreenPrototype> GetAvailable(
        IPrototypeManager prototypes,
        DateTime date,
        bool holidaysEnabled = true)
    {
        var all = prototypes.EnumeratePrototypes<AnimatedLobbyScreenPrototype>().ToList();
        if (!holidaysEnabled)
            return all.Where(s => !s.HasDateRestriction).ToList();

        var seasonal = all.Where(s => s.HasDateRestriction && s.IsAvailable(date)).ToList();
        if (seasonal.Count > 0)
            return seasonal;

        return all.Where(s => !s.HasDateRestriction).ToList();
    }
}
