// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Expedition;

[Serializable, NetSerializable]
public sealed record ExpeditionOfferListing(
    ushort Index,
    int Seed,
    string PlanetName,
    string BiomeId,
    string AirDescription,
    string WeatherDescription,
    int Reward,
    TimeSpan Duration,
    string PresetId,
    string QuestId);

[Serializable, NetSerializable]
public sealed record ExpeditionMissionParams
{
    [ViewVariables]
    public ushort Index;

    [ViewVariables(VVAccess.ReadWrite)]
    public int Seed;

    [ViewVariables(VVAccess.ReadWrite)]
    public string PresetId = string.Empty;

    [ViewVariables(VVAccess.ReadWrite)]
    public string QuestId = string.Empty;

    [ViewVariables(VVAccess.ReadWrite)]
    public int Reward;

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan Duration;
}

[Serializable, NetSerializable]
public sealed class ExpeditionConsoleState : BoundUserInterfaceState
{
    public TimeSpan NextOffer;
    public bool Claimed;
    public bool Cooldown;
    public ushort ActiveMission;
    public List<ExpeditionOfferListing> Missions;
    public bool CanFinish;
    public TimeSpan CooldownTime;
    public int ActiveExpeditionCount;
    public bool IsOurTurnToConfirm;
    public bool HasConfirmDeadline;
    public TimeSpan ConfirmDeadline;
    public bool IsQueued;
    public int QueuePosition;
    public int QueueTotal;
    public bool InCombat;
    public bool MassAllowed;
    public float CurrentMass;
    public float MassLimit;
    public string? BlockReason;
    public bool Enabled;
    public bool Generating;
    public float GenerationProgress;
    public bool HasLandingCoords;
    public int LandingCoordsX;
    public int LandingCoordsY;
    public string LandingCoordCode;

    public ExpeditionConsoleState(
        TimeSpan nextOffer,
        bool claimed,
        bool cooldown,
        ushort activeMission,
        List<ExpeditionOfferListing> missions,
        bool canFinish,
        TimeSpan cooldownTime,
        int activeExpeditionCount,
        bool isOurTurnToConfirm,
        bool hasConfirmDeadline,
        TimeSpan confirmDeadline,
        bool isQueued,
        int queuePosition,
        int queueTotal,
        bool inCombat,
        bool massAllowed,
        float currentMass,
        float massLimit,
        string? blockReason,
        bool enabled,
        bool generating = false,
        float generationProgress = 0f,
        bool hasLandingCoords = false,
        int landingCoordsX = 0,
        int landingCoordsY = 0,
        string landingCoordCode = "")
    {
        NextOffer = nextOffer;
        Claimed = claimed;
        Cooldown = cooldown;
        ActiveMission = activeMission;
        Missions = missions;
        CanFinish = canFinish;
        CooldownTime = cooldownTime;
        ActiveExpeditionCount = activeExpeditionCount;
        IsOurTurnToConfirm = isOurTurnToConfirm;
        HasConfirmDeadline = hasConfirmDeadline;
        ConfirmDeadline = confirmDeadline;
        IsQueued = isQueued;
        QueuePosition = queuePosition;
        QueueTotal = queueTotal;
        InCombat = inCombat;
        MassAllowed = massAllowed;
        CurrentMass = currentMass;
        MassLimit = massLimit;
        BlockReason = blockReason;
        Enabled = enabled;
        Generating = generating;
        GenerationProgress = generationProgress;
        HasLandingCoords = hasLandingCoords;
        LandingCoordsX = landingCoordsX;
        LandingCoordsY = landingCoordsY;
        LandingCoordCode = landingCoordCode;
    }
}

[Serializable, NetSerializable]
public sealed class ClaimExpeditionMessage : BoundUserInterfaceMessage
{
    public ushort Index;
    public int Seed;
}

[Serializable, NetSerializable]
public sealed class ConfirmExpeditionMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CancelExpeditionMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class FinishExpeditionMessage : BoundUserInterfaceMessage;
