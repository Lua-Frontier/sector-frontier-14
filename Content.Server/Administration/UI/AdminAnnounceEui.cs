using Content.Server._Lua.Announcements;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Robust.Shared.Audio;

namespace Content.Server.Administration.UI
{
    public sealed class AdminAnnounceEui : BaseEui
    {
        [Dependency] private readonly IAdminManager _adminManager = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;
        private readonly FactionAnnouncementSystem _factionAnnounce;

        public AdminAnnounceEui()
        {
            IoCManager.InjectDependencies(this);
            _factionAnnounce = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<FactionAnnouncementSystem>();
        }

        public override void Opened()
        {
            StateDirty();
        }

        public override EuiStateBase GetNewState()
        {
            return new AdminAnnounceEuiState
            {
                Factions = [.. _factionAnnounce.GetFactions()],
                Sectors = [.. _factionAnnounce.GetSectors()],
            };
        }

        public override void HandleMessage(EuiMessageBase msg)
        {
            base.HandleMessage(msg);

            switch (msg)
            {
                case AdminAnnounceEuiMsg.DoAnnounce doAnnounce:
                    if (!_adminManager.HasAdminFlag(Player, AdminFlags.Admin))
                    {
                        Close();
                        break;
                    }

                    switch (doAnnounce.AnnounceType)
                    {
                        case AdminAnnounceType.Server:
                            _chatManager.DispatchServerAnnouncement(doAnnounce.Announcement);
                            break;
                        case AdminAnnounceType.Station:
                            _factionAnnounce.TryAnnounce(doAnnounce.Announcement, doAnnounce.FactionId, doAnnounce.SectorId);
                            break;
                        case AdminAnnounceType.Antag:
                            _factionAnnounce.TryAnnounce(
                                doAnnounce.Announcement,
                                doAnnounce.FactionId,
                                doAnnounce.SectorId,
                                new SoundPathSpecifier("/Audio/_Lua/Announcements/war.ogg"));
                            break;
                    }

                    StateDirty();

                    if (doAnnounce.CloseAfter)
                        Close();

                    break;
            }
        }
    }
}
