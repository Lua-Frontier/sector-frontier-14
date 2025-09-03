// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
// Special Permission:
// In addition to AGPLv3, the author grants the "Мёртвый Космос" project
// the right to use this code under a separate custom license agreement.

using Robust.Shared.Audio;

namespace Content.Server._Lua.HardsuitIdentification;

public enum HardsuitSecurityMode
{
    Explode,
    Acid
}

public enum HardsuitIdentificationMode
{
    Registration,
    Clearance,
    Locked
}

[RegisterComponent]
public sealed partial class HardsuitIdentificationComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string DNA = String.Empty;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool DNAWasStored = false;

    [DataField]
    public bool Activated = false;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public HardsuitIdentificationMode IdentificationMode = HardsuitIdentificationMode.Registration;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Emaggable { get; set; } = true;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float EmagSuccessChance { get; set; } = 0.8f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public HardsuitSecurityMode SecurityMode = HardsuitSecurityMode.Explode;

    [DataField]
    public SoundSpecifier SparkSound = new SoundCollectionSpecifier("sparks")
    {
        Params = AudioParams.Default.WithVolume(8),
    };

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int CountdownDuration = 5000;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ExplosionIntensity = 1.0f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float AcidStrength = 1.0f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool AllowMultipleDNA = true;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<string> AuthorizedDNA = new();

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool GibWearer = true;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool CreateAcidEffect = false;
}
