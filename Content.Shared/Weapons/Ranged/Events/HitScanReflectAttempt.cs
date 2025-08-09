// SPDX-FileCopyrightText: 2023 Chief-Engineer
// SPDX-FileCopyrightText: 2023 Slava0135
// SPDX-FileCopyrightText: 2023 metalgearsloth
// SPDX-FileCopyrightText: 2024 Aviu00
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Weapons.Reflect;

namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Shot may be reflected by setting <see cref="Reflected"/> to true
/// and changing <see cref="Direction"/> where shot will go next
/// </summary>
[ByRefEvent]
public record struct HitScanReflectAttemptEvent(EntityUid? Shooter, EntityUid SourceItem, ReflectType Reflective,
    Vector2 Direction, bool Reflected, DamageSpecifier? Damage); // WD EDIT
