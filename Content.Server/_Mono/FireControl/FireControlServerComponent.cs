// Copyright Rane (elijahrane@gmail.com) 2025
// All rights reserved. Relicensed under AGPL with permission

namespace Content.Server._Mono.FireControl;

[RegisterComponent]
public sealed partial class FireControlServerComponent : Component
{
    [ViewVariables]
    public EntityUid? ConnectedGrid = null;

    [ViewVariables]
    public HashSet<EntityUid> Controlled = [];

    [ViewVariables]
    public HashSet<EntityUid> Consoles = [];

    [ViewVariables]
    public Dictionary<EntityUid, EntityUid> Leases;

    [ViewVariables, DataField]
    public int ProcessingPower;

    [ViewVariables]
    public int UsedProcessingPower;
	//Lua start:
    // Salvo firing configuration
    [DataField]
    public bool UseSalvos = true;

    /// <summary>
    /// Period of a salvo cycle in seconds.
    /// </summary>
    [DataField]
    public float SalvoPeriodSeconds = 3f;

    /// <summary>
    /// Duration of salvo fire window in seconds.
    /// </summary>
    [DataField]
    public float SalvoWindowSeconds = 0.5f;

    /// <summary>
    /// Per-weapon additional jitter inside window to avoid perfect sync.
    /// </summary>
    [DataField]
    public float SalvoJitterSeconds = 0.12f;//Lua end
}
