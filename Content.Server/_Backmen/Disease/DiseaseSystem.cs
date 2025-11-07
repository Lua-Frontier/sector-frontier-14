using Content.Server.Backmen.Disease.Components;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Disease;
using Content.Shared.Backmen.Disease.Events;
using Content.Shared.Clothing.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Rejuvenate;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Threading;
using System.Linq;

namespace Content.Server.Backmen.Disease;

public sealed class DiseaseSystem : SharedDiseaseSystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ISerializationManager _serializationManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly IParallelManager _parallel = default!;

    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiseaseCarrierComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<DiseaseCarrierComponent, CureDiseaseAttemptEvent>(OnTryCureDisease);
        SubscribeLocalEvent<DiseaseCarrierComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<DiseasedComponent, ContactInteractionEvent>(OnContactInteraction);
        SubscribeLocalEvent<DiseasedComponent, EntitySpokeEvent>(OnEntitySpeak);
        SubscribeLocalEvent<DiseaseProtectionComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<DiseaseProtectionComponent, GotUnequippedEvent>(OnUnequipped);

        _cfg.OnValueChanged(CCVars.GameDiseaseEnabled, v => _enabled = v, true);
    }

    private bool _enabled = true;

    private readonly HashSet<EntityUid> _addQueue = new();
    private readonly HashSet<(Entity<DiseaseCarrierComponent> carrier, ProtoId<DiseasePrototype> disease)> _cureQueue = new();

    public (int Stage, float LastThreshold) GetStage(DiseasePrototype disease)
    {
        var stage = 0;
        var lastThreshold = 0f;
        for (var j = 0; j < disease.Stages.Count; j++)
        {
            if (!(disease.TotalAccumulator >= disease.Stages[j]) || !(disease.Stages[j] > lastThreshold)) continue;
            lastThreshold = disease.Stages[j];
            stage = j;
        }

        return (stage, lastThreshold);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled)
            return;

        foreach (var entity in _addQueue)
        {
            if (TerminatingOrDeleted(entity))
                continue;

            EnsureComp<DiseasedComponent>(entity);
        }
        _addQueue.Clear();

        foreach (var (carrier, disease) in _cureQueue)
        {
            var d = carrier.Comp.Diseases.FirstOrDefault(x => x.ID == disease);
            if (d != null)
            {
                carrier.Comp.Diseases.Remove(d);
                if (d.Infectious)
                {
                    carrier.Comp.PastDiseases.Add(disease);
                }
            }
            if (carrier.Comp.Diseases.Count == 0) RemCompDeferred<DiseasedComponent>(carrier);
        }
        _cureQueue.Clear();

        var q = EntityQueryEnumerator<DiseasedComponent, DiseaseCarrierComponent, MobStateComponent, MetaDataComponent>();
        while (q.MoveNext(out var owner, out _, out var carrierComp, out var mobState, out var metaDataComponent))
        {
            if (Paused(owner, metaDataComponent))
                continue;

            if (carrierComp.Diseases.Count == 0)
            { continue; }
            _parallel.ProcessNow(new DiseaseJob
            {
                System = this,
                Owner = (owner, carrierComp, mobState),
                FrameTime = frameTime
            },
            carrierComp.Diseases.Count);
        }
    }

    private record struct DiseaseJob : IParallelRobustJob
    {
        public DiseaseSystem System { get; init; }
        public Entity<DiseaseCarrierComponent, MobStateComponent> Owner { get; init; }
        public float FrameTime { get; init; }
        public void Execute(int index)
        { System.Process(Owner, FrameTime, index); }
    }

    private void Process(Entity<DiseaseCarrierComponent, MobStateComponent> owner, float frameTime, int i)
    {
        var disease = owner.Comp1.Diseases[i];
        disease.Accumulator += frameTime;
        disease.TotalAccumulator += frameTime;
        if (disease.Accumulator < disease.TickTime) return;
        var doEffects = owner.Comp1.CarrierDiseases?.Contains(disease.ID) != true;
        disease.Accumulator -= disease.TickTime;

        var (stage, _) = GetStage(disease);

        foreach (var cure in disease.Cures)
        {
            if (!cure.Stages.Contains(stage)) continue;
            try
            { RaiseLocalEvent(owner, cure.GenerateEvent(owner, disease.ID)); }
            catch (Exception err)
            { Log.Error(err.ToString()); }
        }

        if (_mobStateSystem.IsIncapacitated(owner, owner))
            doEffects = false;

        if (!doEffects)
            return;

        foreach (var effect in disease.Effects)
        {
            if (!effect.Stages.Contains(stage) || !_random.Prob(effect.Probability)) continue;
            try
            { RaiseLocalEvent(owner, effect.GenerateEvent(owner, disease.ID)); }
            catch (Exception e)
            { Log.Error(e.ToString()); }

        }
    }

    private void OnInit(EntityUid uid, DiseaseCarrierComponent component, ComponentInit args)
    {
        if (component.NaturalImmunities == null || component.NaturalImmunities.Count == 0)
            return;

        foreach (var immunity in component.NaturalImmunities)
        {
            component.PastDiseases.Add(immunity);
        }
    }
    private void OnTryCureDisease(Entity<DiseaseCarrierComponent> ent, ref CureDiseaseAttemptEvent args)
    {
        foreach (var disease in ent.Comp.Diseases)
        {
            var cureProb = ((args.CureChance / ent.Comp.Diseases.Count) - disease.CureResist);
            if (cureProb < 0) return;
            if (cureProb > 1)
            { CureDisease(ent, disease); return; }
            if (_random.Prob(cureProb))
            { CureDisease(ent, disease); return; }
        }
    }

    private void OnRejuvenate(EntityUid uid, DiseaseCarrierComponent component, RejuvenateEvent args)
    {
        CureAllDiseases(uid, component);
    }

    private void OnEquipped(EntityUid uid, DiseaseProtectionComponent component, GotEquippedEvent args)
    {
        if (!TryComp<ClothingComponent>(uid, out var clothing))
            return;
        if (!clothing.Slots.HasFlag(args.SlotFlags))
            return;
        if (TryComp<DiseaseCarrierComponent>(args.Equipee, out var carrier))
            carrier.DiseaseResist += component.Protection;
        component.IsActive = true;
    }

    private void OnUnequipped(EntityUid uid, DiseaseProtectionComponent component, GotUnequippedEvent args)
    {
        if (!component.IsActive)
            return;
        if (TryComp<DiseaseCarrierComponent>(args.Equipee, out var carrier))
            carrier.DiseaseResist -= component.Protection;
        component.IsActive = false;
    }

    public void CureDisease(Entity<DiseaseCarrierComponent> carrier, DiseasePrototype disease)
    {
        CureDisease(carrier, disease.ID);
    }
    public void CureDisease(Entity<DiseaseCarrierComponent> carrier, ProtoId<DiseasePrototype> disease)
    {
        _cureQueue.Add((carrier, disease));
        _popupSystem.PopupEntity(Loc.GetString("disease-cured"), carrier.Owner, carrier.Owner);
    }

    public void CureAllDiseases(EntityUid uid, DiseaseCarrierComponent? carrier = null)
    {
        if (!Resolve(uid, ref carrier))
            return;

        foreach (var disease in carrier.Diseases)
        {
            CureDisease((uid, carrier), disease);
        }
    }

    private void OnContactInteraction(EntityUid uid, DiseasedComponent component, ContactInteractionEvent args)
    {
        InteractWithDiseased(uid, args.Other);
    }

    private void OnEntitySpeak(EntityUid uid, DiseasedComponent component, EntitySpokeEvent args)
    {
        if (TryComp<DiseaseCarrierComponent>(uid, out var carrier))
        {
            SneezeCough(uid, _random.Pick(carrier.Diseases).ID, string.Empty);
        }
    }

    private void InteractWithDiseased(EntityUid diseased, EntityUid target, DiseaseCarrierComponent? diseasedCarrier = null)
    {
        if (!Resolve(diseased, ref diseasedCarrier, false) ||
            diseasedCarrier.Diseases.Count == 0 ||
            !TryComp<DiseaseCarrierComponent>(target, out var carrier))
            return;

        var disease = _random.Pick(diseasedCarrier.Diseases);
        TryInfect((target, carrier), disease, 0.4f);
    }

    public override void TryAddDisease(EntityUid host, DiseasePrototype addedDisease, DiseaseCarrierComponent? target = null)
    {
        TryAddDisease(host, addedDisease.ID, target);
    }

    public override void TryAddDisease(EntityUid host, ProtoId<DiseasePrototype> addedDisease, DiseaseCarrierComponent? target = null)
    {
        if (!Resolve(host, ref target, false))
            return;

        foreach (var disease in target.AllDiseases)
        {
            if (disease == addedDisease)
                return;
        }

        if (!_prototypeManager.TryIndex(addedDisease, out var added))
            return;
        var freshDisease = _serializationManager.CreateCopy(added, notNullableOverride: true);

        target.Diseases.Add(freshDisease);
        _addQueue.Add(host);
    }

    public void TryInfect(Entity<DiseaseCarrierComponent> carrier, DiseasePrototype? disease, float chance = 0.7f, bool forced = false)
    {
        if (disease == null || !forced && !disease.Infectious)
            return;
        var infectionChance = chance - carrier.Comp.DiseaseResist;
        if (infectionChance <= 0)
            return;
        if (_random.Prob(infectionChance))
            TryAddDisease(carrier.Owner, disease, carrier);
    }

    public void TryInfect(Entity<DiseaseCarrierComponent> carrier, ProtoId<DiseasePrototype>? disease, float chance = 0.7f, bool forced = false)
    {
        if (disease == null || !_prototypeManager.TryIndex(disease, out var d))
            return;

        TryInfect(carrier, d, chance, forced);
    }

    public bool SneezeCough(EntityUid uid, ProtoId<DiseasePrototype> diseaseId, string emoteId, bool airTransmit = true, TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform))
            return false;

        if (_mobStateSystem.IsDead(uid))
            return false;

        var attemptSneezeCoughEvent = new AttemptSneezeCoughEvent(emoteId);
        RaiseLocalEvent(uid, attemptSneezeCoughEvent);
        if (attemptSneezeCoughEvent.Cancelled)
            return false;

        _chatSystem.TryEmoteWithChat(uid, emoteId);

        var disease = _prototypeManager.Index(diseaseId);

        if (disease is not { Infectious: true } || !airTransmit)
            return true;

        if (_inventorySystem.TryGetSlotEntity(uid, "mask", out var maskUid) &&
            TryComp<IngestionBlockerComponent>(maskUid, out var blocker) &&
            blocker.Enabled)
            return true;

        QueueLocalEvent(new DiseaseInfectionSpreadEvent
        {
            Owner = uid,
            Disease = disease,
            Range = 2f
        });

        return true;
    }
}

public sealed class CureDiseaseAttemptEvent(float cureChance) : EntityEventArgs
{
    public float CureChance { get; } = cureChance;
}

public enum SneezeCoughType
{
    Sneeze,
    Cough,
    None
}
