using Content.Server.Body.Components;
using Content.Shared.Backmen.Disease;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Disease.Cures;

public sealed partial class DiseaseReagentCure : DiseaseCure
{
    [DataField("min")]
    public FixedPoint2 Min = 5;
    [DataField("reagent")]
    public ReagentId? Reagent;
    [DataField("cureChance")]
    public float CureChance = 1.0f;

    public override string CureText()
    {
        var prototypeMan = IoCManager.Resolve<IPrototypeManager>();
        if (Reagent == null || !prototypeMan.TryIndex<ReagentPrototype>(Reagent.Value.Prototype, out var reagentProt)) return string.Empty;
        return (Loc.GetString("diagnoser-cure-reagent", ("units", Min), ("reagent", reagentProt.LocalizedName)));
    }

    public override object GenerateEvent(Entity<DiseaseCarrierComponent> ent, ProtoId<DiseasePrototype> disease)
    {
        return new DiseaseCureArgs<DiseaseReagentCure>(ent, disease, this);
    }
}

public sealed partial class DiseaseCureSystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private void DiseaseReagentCure(Entity<DiseaseCarrierComponent> ent, ref DiseaseCureArgs<DiseaseReagentCure> args)
    {
        if(args.Handled) return;
        if (!_bloodstreamQuery.TryGetComponent(ent.Owner, out var bloodstream)) return;
        if (!_solutionContainer.ResolveSolution(ent.Owner, bloodstream.ChemicalSolutionName, ref bloodstream.ChemicalSolution, out var chemicalSolution)) return;
        var quant = FixedPoint2.Zero;
        if (args.DiseaseCure.Reagent != null && chemicalSolution.ContainsReagent(args.DiseaseCure.Reagent.Value))
        {
            quant = chemicalSolution.GetReagentQuantity(args.DiseaseCure.Reagent.Value);
        }

        if (quant >= args.DiseaseCure.Min)
        {
            if (_random.Prob(args.DiseaseCure.CureChance))
            {
                args.Handled = true;
                _disease.CureDisease(ent, args.Disease);
            }
        }
    }
}
