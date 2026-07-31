// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.AmbientSpaceEffects;

[Prototype]
public sealed partial class AmbientSpaceEffectPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Shader { get; private set; } = "AmbientNebula";

    [DataField]
    public float LowerOpacity { get; private set; } = 0.22f;

    [DataField]
    public float MidOpacity { get; private set; } = 0.55f;

    [DataField]
    public float UpperOpacity { get; private set; } = 0.18f;

    [DataField]
    public float LowerParallax { get; private set; } = 0.8f;

    [DataField]
    public float MidParallax { get; private set; } = 1.0f;

    [DataField]
    public float UpperParallax { get; private set; } = 1.2f;

    [DataField]
    public bool MidReactive { get; private set; }

    [DataField]
    public bool UpperReactive { get; private set; }

    [DataField]
    public float UpperReactScale { get; private set; } = 0.4f;

    [DataField]
    public float ParticleScale { get; private set; } = 1f;

    [DataField]
    public float FlowSpeed { get; private set; } = 1f;

    [DataField]
    public float ShipForce { get; private set; } = 0.38f;
}
