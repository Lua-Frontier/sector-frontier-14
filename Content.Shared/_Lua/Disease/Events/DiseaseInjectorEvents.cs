// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;
using Content.Shared.DoAfter;
using Content.Shared._Lua.Disease.Components;

namespace Content.Shared._Lua.Disease.Events;

[Serializable, NetSerializable]
public sealed partial class DiseaseInjectEvent : SimpleDoAfterEvent;

public sealed class DiseaseInjectAttemptEvent(EntityUid user, EntityUid target, EntityUid injector, DiseaseInjectorComponent injectorComp) : CancellableEntityEventArgs
{
    public EntityUid User = user;
    public EntityUid Target = target;
    public EntityUid Injector = injector;
    public DiseaseInjectorComponent InjectorComp = injectorComp;
}
