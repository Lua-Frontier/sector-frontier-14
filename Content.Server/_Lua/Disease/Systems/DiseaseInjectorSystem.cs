// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Diagnostics.CodeAnalysis;
using Content.Shared.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Forensics;
using Content.Shared.Interaction;
using Content.Shared.Body.Components;
using Content.Shared.IdentityManagement;
using Content.Shared._Lua.Disease.Events;
using Content.Shared.Containers.ItemSlots;
using Content.Shared._Lua.Disease.Components;
using Content.Server.Backmen.Disease;
using Content.Server.DoAfter;
using Content.Server.Popups;

namespace Content.Server._Lua.Disease.Systems;

public sealed class DiseaseInjectorSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly DiseaseSystem _disease = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiseaseInjectorComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<DiseaseInjectorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<DiseaseInjectorComponent, DiseaseInjectEvent>(OnInject);
    }

    private void OnExamine(EntityUid injector, DiseaseInjectorComponent injectorComp, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
        {
            return;
        }

        var readyString = injectorComp.UsesLeft == 0 ? "disease-injector-used" : "disease-injector-ready";
        args.PushMarkup(Loc.GetString(readyString));
    }

    private void OnAfterInteract(EntityUid injector, DiseaseInjectorComponent injectorComp, AfterInteractEvent args)
    {
        if (args.Handled
            || !args.CanReach
            || args.Target == null)
        {
            return;
        }

        var target = args.Target.Value;
        var user = args.User;

        if (!CanInject(user, target, injector, injectorComp, out _))
        {
            args.Handled = true;
            return;
        }

        var doAfterArgs = new DoAfterArgs
        (
            EntityManager,
            user,
            injectorComp.InjectTime,
            new DiseaseInjectEvent(),
            injector,
            target,
            injector
        )
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
            BreakOnHandChange = true,
            MovementThreshold = 0.1f,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
        {
            return;
        }

        _popup.PopupEntity(Loc.GetString("disease-injector-injecting-user"), target, user);

        if (target != user)
        {
            var userName = Identity.Entity(user, EntityManager, target);
            _popup.PopupEntity(Loc.GetString("disease-injector-injecting-target", ("user", userName)), user, target, PopupType.LargeCaution);
        }

        args.Handled = true;
    }

    private void OnInject(EntityUid injector, DiseaseInjectorComponent injectorComp, DiseaseInjectEvent args)
    {
        if (args.Cancelled
            || args.Handled
            || args.Target == null
            || args.Used != injector)
        {
            return;
        }

        Inject(args.User, args.Target.Value, injector, injectorComp);

        args.Handled = true;
    }

    private void Inject(EntityUid user, EntityUid target, EntityUid injector, DiseaseInjectorComponent injectorComp)
    {
        if (!CanInject(user, target, injector, injectorComp, out var sampleComp))
        {
            return;
        }

        injectorComp.UsesLeft -= 1;

        if (sampleComp.DiseaseIDs != null)
        {
            foreach (var diseaseID in sampleComp.DiseaseIDs)
            {
                _disease.TryAddDisease(target, diseaseID);
            }
        }

        sampleComp.DiseaseIDs = null;
        _popup.PopupEntity(Loc.GetString("disease-injector-inject-succeed"), injector, user);

        var dnaEv = new TransferDnaEvent
        {
            Donor = target,
            Recipient = injector
        };

        RaiseLocalEvent(target, ref dnaEv);
    }

    private bool CanInject(EntityUid user, EntityUid target, EntityUid injector, DiseaseInjectorComponent injectorComp, [NotNullWhen(true)] out DiseaseContainerComponent? sampleComp)
    {
        sampleComp = null;

        if (!HasComp<BloodstreamComponent>(target))
        {
            return false;
        }

        if (injectorComp.UsesLeft == 0)
        {
            _popup.PopupEntity(Loc.GetString("disease-injector-no-uses-left"), injector, user);
            return false;
        }

        if (_itemSlots.GetItemOrNull(injector, injectorComp.SampleSlotID) is not { } sample)
        {
            _popup.PopupEntity(Loc.GetString("disease-injector-insert-sample"), injector, user);
            return false;
        }

        if (!TryComp(sample, out sampleComp)
            || sampleComp.Fragile)
        {
            _popup.PopupEntity(Loc.GetString("disease-injector-incompatible-sample"), injector, user);
            return false;
        }

        var ev = new DiseaseInjectAttemptEvent(user, target, injector, injectorComp);
        RaiseLocalEvent(target, ev);

        if (ev.Cancelled)
        {
            var targetName = Identity.Name(target, EntityManager, user);
            _popup.PopupEntity(Loc.GetString("disease-injector-inject-failed", ("target", targetName)), target, user);
            return false;
        }

        return true;
    }
}
