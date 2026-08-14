// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Info;

public sealed class SendPublicOfferInformationMessage : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public float PendingRulesPopupTime { get; set; }
    public string PendingCoreRules { get; set; } = string.Empty;
    public bool PendingShouldShowRules { get; set; }

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        PendingRulesPopupTime = buffer.ReadFloat();
        PendingCoreRules = buffer.ReadString();
        PendingShouldShowRules = buffer.ReadBoolean();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(PendingRulesPopupTime);
        buffer.Write(PendingCoreRules);
        buffer.Write(PendingShouldShowRules);
    }
}

public sealed class PublicOfferAcceptedMessage : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
    }
}
