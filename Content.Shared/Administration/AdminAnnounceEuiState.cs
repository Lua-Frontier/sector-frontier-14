using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration
{
    public enum AdminAnnounceType
    {
        Station,
        Server,
        Antag, // Frontier
    }

    [Serializable, NetSerializable]
    public sealed class AdminAnnounceFactionInfo
    {
        public string Id = string.Empty;
        public string Title = string.Empty;
        public Color Color;
    }

    [Serializable, NetSerializable]
    public sealed class AdminAnnounceSectorInfo
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
    }

    [Serializable, NetSerializable]
    public sealed class AdminAnnounceEuiState : EuiStateBase
    {
        public List<AdminAnnounceFactionInfo> Factions = new();
        public List<AdminAnnounceSectorInfo> Sectors = new();
    }

    public static class AdminAnnounceEuiMsg
    {
        [Serializable, NetSerializable]
        public sealed class DoAnnounce : EuiMessageBase
        {
            public bool CloseAfter;
            public string FactionId = string.Empty;
            public string SectorId = string.Empty;
            public string Announcement = default!;
            public AdminAnnounceType AnnounceType;
        }
    }
}
