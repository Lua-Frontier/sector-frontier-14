// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
// Special Permission:
// In addition to AGPLv3, the author grants the "Мёртвый Космос" project
// the right to use this code under a separate custom license agreement.

using Content.Server.Administration.Logs;
using Content.Server.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Popups;
using Content.Shared._Lua.Chat.Systems;
using Content.Shared._Lua.HardsuitIdentification;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Database;
using Content.Shared.Emag.Systems;
using Content.Shared.Forensics.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Lua.HardsuitIdentification;

public sealed class HardsuitIdentificationSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _bodySystem = default!;
    [Dependency] private readonly ExplosionSystem _explosionSystem = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, SecurityData> _activeSecurity = new();

    private struct SecurityData
    {
        public EntityUid Hardsuit;
        public EntityUid Wearer;
        public TimeSpan StartTime;
        public int CurrentStep;
        public float Progress;
        public HardsuitSecurityMode Mode;
    }

    public override void Initialize()
    {
        SubscribeLocalEvent<HardsuitIdentificationComponent, GotEquippedEvent>(OnEquip);
        SubscribeLocalEvent<HardsuitIdentificationComponent, StoreDNAActionEvent>(OnDNAStore);
        SubscribeLocalEvent<HardsuitIdentificationComponent, ClearDNAActionEvent>(OnDNAClear);
        SubscribeLocalEvent<HardsuitIdentificationComponent, LockDNAActionEvent>(OnDNALock);
        SubscribeLocalEvent<HardsuitIdentificationComponent, GotEmaggedEvent>(OnEmagged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        ProcessActiveSecurity(frameTime);
    }

    private void ProcessActiveSecurity(float frameTime)
    {
        var currentTime = _timing.CurTime;
        var toRemove = new List<EntityUid>();
        foreach (var (hardsuit, data) in _activeSecurity)
        {
            if (!EntityManager.EntityExists(hardsuit) || !EntityManager.EntityExists(data.Wearer))
            {
                toRemove.Add(hardsuit);
                continue;
            }
            var elapsed = currentTime - data.StartTime;
            var targetStep = (int)(elapsed.TotalMilliseconds / 1000) + 1;
            var progress = Math.Min(1.0f, (float)(elapsed.TotalMilliseconds / 5000.0));
            var updatedData = data with { Progress = progress };
            if (targetStep > data.CurrentStep && targetStep <= 4)
            {
                SecurityStep(hardsuit, data.Wearer, targetStep, data.Mode);
                updatedData = updatedData with { CurrentStep = targetStep };
            }
            if (progress >= 1.0f)
            {
                SecurityAction(hardsuit, data.Wearer, data.Mode);
                toRemove.Add(hardsuit);
            }
            else
            {
                _activeSecurity[hardsuit] = updatedData;
            }
        }
        foreach (var hardsuit in toRemove)
        {
            _activeSecurity.Remove(hardsuit);
        }
    }

    private void SecurityStep(EntityUid hardsuit, EntityUid wearer, int step, HardsuitSecurityMode mode)
    {
        if (!TryComp<HardsuitIdentificationComponent>(hardsuit, out var comp))
            return;
        switch (step)
        {
            case 1:
                _audio.PlayPvs(comp.SparkSound, hardsuit);
                break;
            case 2:
                _popupSystem.PopupEntity("3", wearer, wearer, Shared.Popups.PopupType.LargeCaution);
                _chat.TrySendInGameICMessage(hardsuit, "3", InGameICChatType.Speak, true);
                _audio.PlayPvs(comp.SparkSound, hardsuit);
                break;
            case 3:
                _popupSystem.PopupEntity("2", wearer, wearer, Shared.Popups.PopupType.LargeCaution);
                _chat.TrySendInGameICMessage(hardsuit, "2", InGameICChatType.Speak, true);
                _audio.PlayPvs(comp.SparkSound, hardsuit);
                break;
            case 4:
                _popupSystem.PopupEntity("1", wearer, wearer, Shared.Popups.PopupType.LargeCaution);
                _chat.TrySendInGameICMessage(hardsuit, "1", InGameICChatType.Speak, true);
                _audio.PlayPvs(comp.SparkSound, hardsuit);
                break;
        }
    }

    private void SecurityAction(EntityUid hardsuit, EntityUid wearer, HardsuitSecurityMode mode)
    {
        if (!EntityManager.EntityExists(hardsuit))
            return;
        var comp = Comp<HardsuitIdentificationComponent>(hardsuit);
        switch (mode)
        {
            case HardsuitSecurityMode.Explode:
                Explosion(hardsuit, wearer, comp);
                break;
            case HardsuitSecurityMode.Acid:
                Acidifier(hardsuit, wearer, comp);
                break;
        }
    }

    private void Explosion(EntityUid hardsuit, EntityUid wearer, HardsuitIdentificationComponent comp)
    {
        var intensity = 4 * comp.ExplosionIntensity;
        _explosionSystem.QueueExplosion(hardsuit, ExplosionSystem.DefaultExplosionPrototypeId,
            intensity, 1, 2, maxTileBreak: 0);
        if (comp.GibWearer && TryComp<BodyComponent>(wearer, out var body) &&
            ((_inventory.TryGetSlotEntity(wearer, "outerClothing", out var hardsuitEntity) && hardsuitEntity == hardsuit) ||
             (_inventory.TryGetSlotEntity(wearer, "back", out var backpackEntity) && backpackEntity == hardsuit)))
        {
            var ents = _bodySystem.GibBody(wearer, true, body, false);
            foreach (var part in ents)
            {
                if (HasComp<BodyPartComponent>(part))
                {
                    QueueDel(part);
                }
            }
        }
        EntityManager.DeleteEntity(hardsuit);
    }

    private void Acidifier(EntityUid hardsuit, EntityUid wearer, HardsuitIdentificationComponent comp)
    {
        _popupSystem.PopupEntity(Loc.GetString("hardsuit-acidifier-dissolve"), wearer, wearer);
        if (comp.CreateAcidEffect)
        {
            var coords = Transform(hardsuit).Coordinates;
            EntityManager.SpawnEntity("Acidifier", coords);
        }
        _audio.PlayPvs(comp.SparkSound, hardsuit);
        ScheduleAcidDestruction(hardsuit, wearer, comp);
    }

    private void ScheduleAcidDestruction(EntityUid hardsuit, EntityUid wearer, HardsuitIdentificationComponent comp)
    {
        for (int i = 0; i < 3; i++)
        {
            var delay = i * 500;
            Timer.Spawn(delay, () =>
            {
                if (EntityManager.EntityExists(hardsuit))
                {
                    _popupSystem.PopupEntity(Loc.GetString("hardsuit-acid"), hardsuit, wearer);
                    if (comp.CreateAcidEffect)
                    {
                        var coords = Transform(hardsuit).Coordinates;
                        EntityManager.SpawnEntity("Acidifier", coords);
                    }
                    _audio.PlayPvs(comp.SparkSound, hardsuit);
                }
            });
        }
        Timer.Spawn(1500, () =>
        {
            if (EntityManager.EntityExists(hardsuit))
            {
                if (comp.CreateAcidEffect)
                {
                    var coords = Transform(hardsuit).Coordinates;
                    EntityManager.SpawnEntity("Acidifier", coords);
                }
                EntityManager.DeleteEntity(hardsuit);
            }
        });
    }

    public void OnEquip(EntityUid uid, HardsuitIdentificationComponent comp, GotEquippedEvent args)
    {
        if (comp.Activated)
            return;
        switch (comp.IdentificationMode)
        {
            case HardsuitIdentificationMode.Registration:
                return;
            case HardsuitIdentificationMode.Clearance:
                if (IsAuthorizedUser(args.Equipee, comp))
                    return;
                break;
            case HardsuitIdentificationMode.Locked:
                if (IsAuthorizedUser(args.Equipee, comp))
                    return;
                break;
        }
        StartSecuritySequence(uid, comp, args.Equipment, args.Equipee);
    }

    private bool IsAuthorizedUser(EntityUid equipee, HardsuitIdentificationComponent comp)
    {
        if (!TryComp(equipee, out DnaComponent? dna))
            return false;
        if (comp.AllowMultipleDNA)
        {
            return comp.AuthorizedDNA.Contains(dna.DNA ?? string.Empty);
        }
        else
        {
            return comp.DNA == (dna.DNA ?? string.Empty);
        }
    }

    private void StartSecuritySequence(EntityUid uid, HardsuitIdentificationComponent comp, EntityUid equipment, EntityUid equipee)
    {
        comp.Activated = true;
        var modeText = comp.SecurityMode == HardsuitSecurityMode.Explode ? "explosion" : "Acidifier";
        _adminLogger.Add(LogType.Trigger, LogImpact.Medium,
            $"{ToPrettyString(equipee):user} activated hardsuit {modeText} system of {ToPrettyString(equipment):target}");
        EnsureComp<UnremoveableComponent>(equipment);
        var spikeMsg = comp.SecurityMode == HardsuitSecurityMode.Explode
            ? Loc.GetString("hardsuit-identification-error-spikes")
            : Loc.GetString("hardsuit-acidifier-error-spikes");
        _popupSystem.PopupEntity(spikeMsg, equipee, equipee, Shared.Popups.PopupType.Large);
        var countdownStartMsg = comp.SecurityMode == HardsuitSecurityMode.Explode
            ? Loc.GetString("hardsuit-identification-countdown-start")
            : Loc.GetString("hardsuit-acidifier-countdown-start");
        _popupSystem.PopupEntity(countdownStartMsg, equipee, equipee, Shared.Popups.PopupType.LargeCaution);
        _chat.TrySendInGameICMessage(equipment, countdownStartMsg, InGameICChatType.Speak, true);
        _activeSecurity[equipment] = new SecurityData
        {
            Hardsuit = equipment,
            Wearer = equipee,
            StartTime = _timing.CurTime,
            CurrentStep = 0,
            Progress = 0.0f,
            Mode = comp.SecurityMode
        };
    }

    public void OnDNAStore(EntityUid uid, HardsuitIdentificationComponent comp, StoreDNAActionEvent args)
    {
        if (args.Handled)
            return;
        if (comp.IdentificationMode != HardsuitIdentificationMode.Registration)
        {
            _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-wrong-mode"), args.Performer, args.Performer);
            args.Handled = true;
            return;
        }
        if (comp.DNAWasStored && !comp.AllowMultipleDNA)
        {
            _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-dna-already-stored"), args.Performer, args.Performer);
        }
        else if (TryComp(args.Performer, out DnaComponent? dna))
        {
            if (comp.AllowMultipleDNA)
            {
                if (!comp.AuthorizedDNA.Contains(dna.DNA ?? string.Empty))
                {
                    comp.AuthorizedDNA.Add(dna.DNA ?? string.Empty);
                    _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-dna-added"), args.Performer, args.Performer);
                }
                else
                {
                    _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-dna-already-stored"), args.Performer, args.Performer);
                }
            }
            else
            {
                comp.DNA = dna.DNA ?? string.Empty;
                _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-dna-was-stored"), args.Performer, args.Performer);
            }
            comp.DNAWasStored = true;
            Dirty(uid, comp);
        }
        else
        {
            _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-dna-not-presented"), args.Performer, args.Performer);
        }
        args.Handled = true;
    }

    public void OnDNAClear(EntityUid uid, HardsuitIdentificationComponent comp, ClearDNAActionEvent args)
    {
        if (args.Handled)
            return;
        if (!IsAuthorizedUser(args.Performer, comp))
        {
            _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-unauthorized"), args.Performer, args.Performer);
            args.Handled = true;
            return;
        }
        if (comp.AllowMultipleDNA && TryComp(args.Performer, out DnaComponent? dna))
        {
            comp.AuthorizedDNA.Remove(dna.DNA ?? string.Empty);
            if (comp.AuthorizedDNA.Count == 0)
            {
                comp.DNA = string.Empty;
                comp.DNAWasStored = false;
                comp.IdentificationMode = HardsuitIdentificationMode.Registration;
                _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-dna-cleared"), args.Performer, args.Performer);
            }
            else
            {
                _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-dna-removed"), args.Performer, args.Performer);
            }
        }
        else
        {
            comp.DNA = string.Empty;
            comp.AuthorizedDNA.Clear();
            comp.DNAWasStored = false;
            comp.IdentificationMode = HardsuitIdentificationMode.Registration;
            _popupSystem.PopupEntity("hardsuit-identification-dna-cleared", args.Performer, args.Performer);
        }
        Dirty(uid, comp);
        args.Handled = true;
    }

    public void OnDNALock(EntityUid uid, HardsuitIdentificationComponent comp, LockDNAActionEvent args)
    {
        if (args.Handled)
            return;
        if (comp.IdentificationMode != HardsuitIdentificationMode.Registration)
        {
            _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-wrong-mode"), args.Performer, args.Performer);
            args.Handled = true;
            return;
        }
        if (!comp.DNAWasStored)
        {
            _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-no-dna-stored"), args.Performer, args.Performer);
            args.Handled = true;
            return;
        }
        if (!IsAuthorizedUser(args.Performer, comp))
        {
            _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-unauthorized"), args.Performer, args.Performer);
            args.Handled = true;
            return;
        }
        comp.IdentificationMode = HardsuitIdentificationMode.Locked;
        Dirty(uid, comp);
        _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-locked"), args.Performer, args.Performer);
        args.Handled = true;
    }

    public void OnEmagged(EntityUid uid, HardsuitIdentificationComponent comp, GotEmaggedEvent args)
    {
        if (!comp.Emaggable)
        {
            _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-emag-denied"), uid);
            args.Handled = true;
            return;
        }
        var random = new Random();
        if (random.NextDouble() > comp.EmagSuccessChance)
        {
            _popupSystem.PopupEntity(Loc.GetString("hardsuit-identification-emag-failed"), uid);
            _audio.PlayPvs(comp.SparkSound, uid);
            args.Handled = true;
            return;
        }
        _audio.PlayPvs(comp.SparkSound, uid);
        var message = comp.Activated
            ? Loc.GetString("hardsuit-identification-on-emagged-late")
            : Loc.GetString("hardsuit-identification-on-emagged");
        _popupSystem.PopupEntity(message, uid);
        _activeSecurity.Remove(uid);
        EntityManager.RemoveComponent<HardsuitIdentificationComponent>(uid);
        args.Handled = true;
    }
}
