// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Collections.Generic;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Achievements;

public sealed class RequestAchievementsMessage : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
    }
}

public sealed class AchievementsStateMessage : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public List<AchievementEntry> Entries = new();

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var count = buffer.ReadVariableInt32();
        Entries = new List<AchievementEntry>(count);

        for (var i = 0; i < count; i++)
        {
            Entries.Add(new AchievementEntry
            {
                AchievementId = buffer.ReadString(),
                Unlocked = buffer.ReadBoolean(),
                UnlockedAtTicks = buffer.ReadBoolean() ? buffer.ReadInt64() : null,
                Progress = buffer.ReadVariableInt32(),
                ProgressMax = buffer.ReadVariableInt32(),
                RewardClaimed = buffer.ReadBoolean(),
            });
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.WriteVariableInt32(Entries.Count);

        foreach (var entry in Entries)
        {
            buffer.Write(entry.AchievementId);
            buffer.Write(entry.Unlocked);
            buffer.Write(entry.UnlockedAtTicks.HasValue);
            if (entry.UnlockedAtTicks.HasValue)
                buffer.Write(entry.UnlockedAtTicks.Value);
            buffer.WriteVariableInt32(entry.Progress);
            buffer.WriteVariableInt32(entry.ProgressMax);
            buffer.Write(entry.RewardClaimed);
        }
    }
}

public sealed class ClaimAchievementRewardMessage : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public string AchievementId = string.Empty;

    public ClaimAchievementRewardMessage()
    {
    }

    public ClaimAchievementRewardMessage(string achievementId)
    {
        AchievementId = achievementId;
    }

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        AchievementId = buffer.ReadString();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(AchievementId);
    }
}

public sealed class AchievementRewardClaimedMessage : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public string AchievementId = string.Empty;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        AchievementId = buffer.ReadString();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(AchievementId);
    }
}

public sealed class AchievementUnlockedMessage : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public string AchievementId = string.Empty;
    public long UnlockedAtTicks;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        AchievementId = buffer.ReadString();
        UnlockedAtTicks = buffer.ReadInt64();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(AchievementId);
        buffer.Write(UnlockedAtTicks);
    }
}

public sealed class AchievementProgressMessage : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public string AchievementId = string.Empty;
    public int Progress;
    public int ProgressMax;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        AchievementId = buffer.ReadString();
        Progress = buffer.ReadVariableInt32();
        ProgressMax = buffer.ReadVariableInt32();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(AchievementId);
        buffer.WriteVariableInt32(Progress);
        buffer.WriteVariableInt32(ProgressMax);
    }
}

[Serializable]
public sealed class TryUnlockAchievementMessage : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public string AchievementId = string.Empty;

    public TryUnlockAchievementMessage()
    {
    }

    public TryUnlockAchievementMessage(string achievementId)
    {
        AchievementId = achievementId;
    }

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        AchievementId = buffer.ReadString();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(AchievementId);
    }
}

[Serializable, NetSerializable]
public sealed class AchievementEntry
{
    public string AchievementId = string.Empty;
    public bool Unlocked;
    public long? UnlockedAtTicks;
    public int Progress;
    public int ProgressMax;
    public bool RewardClaimed;
}
