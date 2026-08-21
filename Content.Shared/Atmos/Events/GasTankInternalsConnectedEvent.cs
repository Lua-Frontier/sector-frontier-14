// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

namespace Content.Shared.Atmos.Events;

[ByRefEvent]
public readonly record struct GasTankInternalsConnectedEvent(EntityUid Tank, EntityUid InternalsOwner, EntityUid? User);
