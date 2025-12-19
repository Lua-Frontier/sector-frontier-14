using Content.Server.Database;
using Content.Server.Players.JobWhitelist;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using System;
using System.Threading.Tasks;

namespace Content.Server.Sponsors;

public sealed class SponsorManager : IPostInjectInit
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly UserDbDataManager _userDb = default!;
    [Dependency] private readonly JobWhitelistManager _jobWhitelist = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;
    private static readonly ProtoId<JobPrototype>[] ShareholderJobIds =
    {
        "Vip",
        "OutpostSyndicateShareholder"
    };
    private readonly Dictionary<NetUserId, Sponsor> _activeSponsors = new();

    void IPostInjectInit.PostInject()
    {
        _sawmill = _logManager.GetSawmill("sponsor");
        _userDb.AddOnFinishLoad(OnFinishLoad);
        _userDb.AddOnPlayerDisconnect(OnPlayerDisconnect);
    }

    public async Task AddSponsorAsync(NetUserId userId, string playerName, string role, DateTimeOffset? plannedEnd)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            await _db.AddOrUpdateSponsor(userId.UserId, playerName, role, now.UtcDateTime, plannedEnd?.UtcDateTime);
            foreach (var jobId in ShareholderJobIds)
            { _jobWhitelist.AddWhitelist(userId, jobId); }
            _sawmill.Info("Added sponsor {UserId} ({Name}) with role {Role} starting at {Start} planned end {End}", userId, playerName, role, now, plannedEnd);
        }
        catch (Exception e)
        { _sawmill.Error($"Failed to add sponsor for player {userId}: {e}"); }
    }

    public async Task RemoveSponsorAsync(NetUserId userId, string role, DateTimeOffset endDate)
    {
        try
        {
            await _db.CloseSponsor(userId.UserId, role, endDate.UtcDateTime);
            foreach (var jobId in ShareholderJobIds)
            { _jobWhitelist.RemoveWhitelist(userId, jobId); }
            _sawmill.Info("Removed sponsor {UserId} with role {Role} at {End}", userId, role, endDate);
        }
        catch (Exception e)
        { _sawmill.Error($"Failed to remove sponsor for player {userId}: {e}"); }
    }

    public async Task RemoveSponsorAsync(NetUserId userId)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var sponsor = await _db.GetActiveSponsor(userId.UserId);
            if (sponsor == null) return;
            await _db.CloseSponsor(userId.UserId, sponsor.Role, now.UtcDateTime);
            foreach (var jobId in ShareholderJobIds)
            { _jobWhitelist.RemoveWhitelist(userId, jobId); }
            _sawmill.Info("Removed sponsor {UserId} with role {Role} at {End} via simple remove", userId, sponsor.Role, now);
        }
        catch (Exception e)
        { _sawmill.Error($"Failed to remove sponsor for player {userId}: {e}"); }
    }

    private async void OnFinishLoad(ICommonSession session)
    {
        try
        {
            var sponsor = await _db.GetActiveSponsor(session.UserId.UserId);
            if (sponsor != null)
            {
                _activeSponsors[session.UserId] = sponsor;
                foreach (var jobId in ShareholderJobIds)
                { _jobWhitelist.AddWhitelist(session.UserId, jobId); }
            }
            else
            {
                _activeSponsors.Remove(session.UserId);
                foreach (var jobId in ShareholderJobIds)
                {
                    var isWhitelisted = await _db.IsJobWhitelisted(session.UserId.UserId, jobId);
                    if (isWhitelisted)
                    { _jobWhitelist.RemoveWhitelist(session.UserId, jobId); }
                }
            }
        }
        catch (Exception e)
        { _sawmill.Error($"Failed to sync sponsor state for player {session.UserId}: {e}"); }
    }

    private void OnPlayerDisconnect(ICommonSession session)
    { _activeSponsors.Remove(session.UserId); }

    public bool TryGetActiveSponsor(NetUserId userId, out Sponsor sponsor)
    { return _activeSponsors.TryGetValue(userId, out sponsor!); }
}


