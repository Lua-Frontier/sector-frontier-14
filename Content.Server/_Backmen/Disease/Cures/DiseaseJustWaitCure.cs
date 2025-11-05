using Content.Shared.Backmen.Disease;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Disease.Cures;

public sealed partial class DiseaseJustWaitCure : DiseaseCure
{
    [ViewVariables(VVAccess.ReadWrite)]
    public int Ticker = 0;
    [DataField("maxLength", required: true)]
    [ViewVariables(VVAccess.ReadWrite)]
    public int MaxLength = 150;

    public override string CureText()
    {
        return Loc.GetString("diagnoser-cure-wait", ("time", MaxLength));
    }

    public override object GenerateEvent(Entity<DiseaseCarrierComponent> ent, ProtoId<DiseasePrototype> disease)
    {
        return new DiseaseCureArgs<DiseaseJustWaitCure>(ent, disease, this);
    }
}

public sealed partial class DiseaseCureSystem
{
    private void DiseaseJustWaitCure(Entity<DiseaseCarrierComponent> ent, ref DiseaseCureArgs<DiseaseJustWaitCure> args)
    {
        if(args.Handled)
            return;

        args.DiseaseCure.Ticker++;
        if (args.DiseaseCure.Ticker >= args.DiseaseCure.MaxLength)
        {
            args.Handled = true;
            _disease.CureDisease(ent, args.Disease);
        }
    }
}
