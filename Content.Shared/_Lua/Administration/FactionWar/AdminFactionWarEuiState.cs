using Content.Shared._Lua.Company;
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Administration.FactionWar;

[Serializable, NetSerializable]
public sealed class AdminFactionWarEuiState : EuiStateBase
{
    public List<CompanyWarOverview> ActiveWars;
    public string StatusText;

    public AdminFactionWarEuiState(List<CompanyWarOverview> activeWars, string statusText)
    {
        ActiveWars = activeWars;
        StatusText = statusText;
    }
}

public static class AdminFactionWarEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class RefreshRequest : EuiMessageBase;

    [Serializable, NetSerializable]
    public sealed class ForceDeclareRequest : EuiMessageBase
    {
        public string AggressorCompanyId;
        public string DefenderCompanyId;
        public string AnnouncementText;

        public ForceDeclareRequest(string aggressorCompanyId, string defenderCompanyId, string announcementText)
        {
            AggressorCompanyId = aggressorCompanyId;
            DefenderCompanyId = defenderCompanyId;
            AnnouncementText = announcementText;
        }
    }

    [Serializable, NetSerializable]
    public sealed class ForceEndRequest : EuiMessageBase
    {
        public int WarId;

        public ForceEndRequest(int warId)
        {
            WarId = warId;
        }
    }
}
