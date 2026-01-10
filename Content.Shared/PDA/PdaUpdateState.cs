using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared.PDA
{
    [Serializable, NetSerializable]
    public sealed class PdaUpdateState : CartridgeLoaderUiState
    {
        public bool FlashlightEnabled;
        public bool HasPen;
        public bool HasPai;
        public bool HasBook;
        public PdaIdInfoText PdaOwnerInfo;
        public string? StationName;
        public bool HasUplink;
        public bool CanPlayMusic;
        public string? Address;
        public int Balance; // Frontier
        public string? OwnedShipName; // Frontier
        public string? AssignedShipName; // Lua
        public string? AssignedShipRoleLocKey; // Lua

        public PdaUpdateState(
            List<NetEntity> programs,
            NetEntity? activeUI,
            bool flashlightEnabled,
            bool hasPen,
            bool hasPai,
            bool hasBook,
            PdaIdInfoText pdaOwnerInfo,
            int balance, // Frontier
            string? ownedShipName, // Frontier
            string? assignedShipName, // Lua
            string? assignedShipRoleLocKey, // Lua
            string? stationName,
            bool hasUplink = false,
            bool canPlayMusic = false,
            string? address = null)
            : base(programs, activeUI)
        {
            FlashlightEnabled = flashlightEnabled;
            HasPen = hasPen;
            HasPai = hasPai;
            HasBook = hasBook;
            PdaOwnerInfo = pdaOwnerInfo;
            HasUplink = hasUplink;
            CanPlayMusic = canPlayMusic;
            StationName = stationName;
            Address = address;
            Balance = balance; // Frontier
            OwnedShipName = ownedShipName; // Frontier
            AssignedShipName = assignedShipName; // Lua
            AssignedShipRoleLocKey = assignedShipRoleLocKey; // Lua
        }
    }

    [Serializable, NetSerializable]
    public struct PdaIdInfoText
    {
        public string? ActualOwnerName;
        public string? IdOwner;
        public string? JobTitle;
        public string? StationAlertLevel;
        public Color StationAlertColor;
    }
}
