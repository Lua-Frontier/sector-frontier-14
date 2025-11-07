using Content.Server.Temperature.Components;
using Content.Shared.Backmen.Disease;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Disease.Cures;

public sealed partial class DiseaseBodyTemperatureCure : DiseaseCure
{
    [DataField("min")]
    public float Min = 0;

    [DataField("max")]
    public float Max = float.MaxValue;

    public override string CureText()
    {
        if (Min == 0)
            return Loc.GetString("diagnoser-cure-temp-max", ("max", Math.Round(Max)));
        if (Max == float.MaxValue)
            return Loc.GetString("diagnoser-cure-temp-min", ("min", Math.Round(Min)));

        return Loc.GetString("diagnoser-cure-temp-both", ("max", Math.Round(Max)), ("min", Math.Round(Min)));
    }

    public override object GenerateEvent(Entity<DiseaseCarrierComponent> ent, ProtoId<DiseasePrototype> disease)
    {
        return new DiseaseCureArgs<DiseaseBodyTemperatureCure>(ent, disease, this);
    }
}

public sealed partial class DiseaseCureSystem
{
    private void DiseaseBodyTemperatureCure(Entity<DiseaseCarrierComponent> ent, ref DiseaseCureArgs<DiseaseBodyTemperatureCure> args)
    {
        if(args.Handled)
            return;

        if (!_temperatureQuery.TryGetComponent(ent.Owner, out var temp))
            return;

        if(temp.CurrentTemperature > args.DiseaseCure.Min && temp.CurrentTemperature < float.MaxValue)
        {
            args.Handled = true;
            _disease.CureDisease(ent, args.Disease);
        }
    }
}
