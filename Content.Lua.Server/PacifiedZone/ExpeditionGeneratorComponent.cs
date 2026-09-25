// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using System.Collections.Generic;
using Robust.Shared.Prototypes;
using Content.Shared.Roles;

namespace Content.Server._NF.ExpeditionZone
{
    [RegisterComponent]
    public sealed partial class ExpeditionZoneGeneratorComponent : Component
    {
        [ViewVariables]
        public HashSet<EntityUid> TrackedEntities = new();

        [ViewVariables]
        public TimeSpan NextUpdate;

        [DataField]
        public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

        [DataField]
        public int Radius = 5;
    }
}
