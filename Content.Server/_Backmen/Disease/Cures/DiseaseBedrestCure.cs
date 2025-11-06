using Content.Shared.Backmen.Disease;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Disease.Cures;

public sealed partial class DiseaseBedrestCure : DiseaseCure
{
    [ViewVariables(VVAccess.ReadWrite)]
    public int Ticker = 0;

    /// How many extra ticks you get for sleeping.
    [DataField("sleepMultiplier")]
    public int SleepMultiplier = 3;

    [DataField("maxLength", required: true)]
    [ViewVariables(VVAccess.ReadWrite)]
    public int MaxLength = 60;

    public override string CureText()
    {
        return (Loc.GetString("diagnoser-cure-bedrest", ("time", MaxLength), ("sleep", (MaxLength / SleepMultiplier))));
    }

    public override object GenerateEvent(Entity<DiseaseCarrierComponent> ent, ProtoId<DiseasePrototype> disease)
    {
        return new DiseaseCureArgs<DiseaseBedrestCure>(ent, disease, this);
    }
}

public sealed partial class DiseaseCureSystem
{
    private void DiseaseBedrestCure(Entity<DiseaseCarrierComponent> ent, ref DiseaseCureArgs<DiseaseBedrestCure> args)
    {
        if(args.Handled)
            return;

        if (!_buckleQuery.TryGetComponent(ent.Owner, out var buckle) ||
            !_healOnBuckleQuery.HasComponent(buckle.BuckledTo))
            return;

        var ticks = 1;
        if (_sleepingComponentQuery.HasComponent(ent.Owner))
            ticks *= args.DiseaseCure.SleepMultiplier;

        if (buckle.Buckled)
            args.DiseaseCure.Ticker += ticks;
        if (args.DiseaseCure.Ticker >= args.DiseaseCure.MaxLength)
        {
            args.Handled = true;
            _disease.CureDisease(ent, args.Disease);
        }
    }
}
