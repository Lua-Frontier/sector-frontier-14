using Content.Shared._Mono.Company;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Players;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Company;

/// <summary>
/// This system handles assigning a company to players when they join.
/// </summary>
public sealed class CompanySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedJobSystem _job = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;

    // Dictionary to store original company preferences for players
    private readonly Dictionary<string, string> _playerOriginalCompanies = new();

    private readonly HashSet<string> _ngcJobs = new()
    {
        "Sheriff",
        // "StationRepresentative", Lua off
        // "StationTrafficController", Lua off
        "Bailiff",
        "SeniorOfficer", // Sergeant
        "Deputy",
        "Brigmedic",
        "NFDetective" // Lua ,<
        //"PublicAffairsLiaison", // Lua off
        // "DirectorOfCare" // Lua off
    };

    private readonly HashSet<string> _rogueJobs = new()
    {
        "NFPirateCaptain",
        "NFPirateFirstMate",
        "NFPirate"
    };

    private HashSet<ProtoId<NpcFactionPrototype>>? _companyNpcFactions;

    public override void Initialize()
    {
        base.Initialize();

        // Subscribe to player spawn event to add the company component
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);

        // Subscribe to examination to show the company on examine
        SubscribeLocalEvent<Shared._Mono.Company.CompanyComponent, ExaminedEvent>(OnExamined);

        // Subscribe to player detached event to clean up stored preferences
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnPlayerDetached(PlayerDetachedEvent args)
    {
        // Clean up stored preferences when player disconnects
        _playerOriginalCompanies.Remove(args.Player.UserId.ToString());
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        // Add the company component with the player's saved company
        var companyComp = EnsureComp<Shared._Mono.Company.CompanyComponent>(args.Mob);

        var playerId = args.Player.UserId.ToString();
        var profileCompany = args.Profile.Company;

        // Lua first check specials
        if (!string.IsNullOrEmpty(companyComp.CompanyName) && companyComp.CompanyName != "None")
        {
            SetCompany(args.Mob, companyComp.CompanyName, companyComp);
            return;
        }
        // Lua first check specials

        // Use "None" as fallback for empty company
        if (string.IsNullOrEmpty(profileCompany))
            profileCompany = "None";

        // Store the player's original company preference if not already stored
        if (!_playerOriginalCompanies.ContainsKey(playerId))
        {
            _playerOriginalCompanies[playerId] = profileCompany;
        }

        string assignedCompany;

        // Check if player's job is one of the NGC jobs
        if (args.JobId != null && _ngcJobs.Contains(args.JobId))
        {
            // Assign NGC company
            assignedCompany = "Security"; // Lua NGC<Security
        }
        // Check if player's job is one of the Rogue jobs
        else if (args.JobId != null && _rogueJobs.Contains(args.JobId))
        {
            // Assign Rogue company
            assignedCompany = "None"; // Lua Rogue<None
        }
        else
        {
            // Restore the player's original company preference
            assignedCompany = _playerOriginalCompanies[playerId];
        }

        // Lua start: Login support
        if (assignedCompany == "None")
        {
            foreach (var companyProto in _prototypeManager.EnumeratePrototypes<CompanyPrototype>())
            {
                if (companyProto.Logins.Contains(args.Player.Name))
                {
                    assignedCompany = companyProto.ID;
                    break;
                }
            }
        }
        // Lua end

        SetCompany(args.Mob, assignedCompany, companyComp);
    }

    public void SetCompany(EntityUid uid, string companyId, CompanyComponent? companyComp = null)
    {
        companyComp ??= EnsureComp<CompanyComponent>(uid);
        var oldCompanyId = string.IsNullOrWhiteSpace(companyComp.CompanyName) ? "None" : companyComp.CompanyName;
        var changed = !string.Equals(oldCompanyId, companyId, StringComparison.OrdinalIgnoreCase);

        if (changed
            && TryComp<CompanyRevealComponent>(uid, out var revealComp))
        {
            revealComp.RevealedToPlayerIds.Clear();
        }

        companyComp.CompanyName = companyId;
        Dirty(uid, companyComp);
        SyncNpcFactions(uid, companyId);

        RaiseLocalEvent(uid, new CompanySetEvent(oldCompanyId, companyId, changed));
    }

    public void UpdateStoredCompanyPreference(EntityUid uid, string companyId)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        _playerOriginalCompanies[actor.PlayerSession.UserId.ToString()] = companyId;
    }

    public bool CanSeeCompany(EntityUid target, EntityUid examiner, CompanyComponent? targetCompany = null)
    {
        if (!Resolve(target, ref targetCompany, false))
            return false;

        if (string.IsNullOrWhiteSpace(targetCompany.CompanyName) || targetCompany.CompanyName == "None")
            return false;

        if (string.Equals(GetCompanyId(examiner), targetCompany.CompanyName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (IsCompanyPubliclyKnown(targetCompany.CompanyName))
            return true;

        return IsCompanyRevealedTo(target, examiner);
    }

    public bool NeedsFactionRevealRequest(EntityUid target, EntityUid examiner, CompanyComponent? targetCompany = null)
    {
        if (!Resolve(target, ref targetCompany, false))
            return false;

        if (string.IsNullOrWhiteSpace(targetCompany.CompanyName) || targetCompany.CompanyName == "None")
            return false;

        if (IsCompanyPubliclyKnown(targetCompany.CompanyName))
            return false;

        return !CanSeeCompany(target, examiner, targetCompany);
    }

    public void RevealCompanyTo(EntityUid target, ICommonSession session)
    {
        var comp = EnsureComp<CompanyRevealComponent>(target);
        comp.RevealedToPlayerIds.Add(session.UserId.ToString());
    }

    public string GetVisibleCompanyMarkup(EntityUid target, EntityUid examiner, CompanyComponent? targetCompany = null)
    {
        if (!Resolve(target, ref targetCompany, false))
            return Loc.GetString("company-examine-unknown");

        if (string.IsNullOrWhiteSpace(targetCompany.CompanyName) || targetCompany.CompanyName == "None")
            return Loc.GetString("company-examine-unknown");

        if (!CanSeeCompany(target, examiner, targetCompany))
            return Loc.GetString("company-examine-unknown");

        if (_prototypeManager.TryIndex<CompanyPrototype>(targetCompany.CompanyName, out var prototype))
            return $"[color={prototype.Color.ToHex()}]{prototype.Name}[/color]";

        return $"[color=yellow]{targetCompany.CompanyName}[/color]";
    }

    private void SyncNpcFactions(EntityUid uid, string? companyId)
    {
        var managedFactions = GetCompanyNpcFactions();
        var targetFactions = GetTargetNpcFactions(companyId);

        if (!TryComp(uid, out NpcFactionMemberComponent? npcFactionComp) && targetFactions.Count == 0)
            return;

        foreach (var faction in managedFactions)
        {
            _npcFaction.RemoveFaction(uid, faction);
        }

        foreach (var faction in targetFactions)
        {
            _npcFaction.AddFaction(uid, faction);
        }
    }

    private HashSet<ProtoId<NpcFactionPrototype>> GetCompanyNpcFactions()
    {
        if (_companyNpcFactions != null)
            return _companyNpcFactions;

        _companyNpcFactions = new HashSet<ProtoId<NpcFactionPrototype>>();

        foreach (var prototype in _prototypeManager.EnumeratePrototypes<CompanyPrototype>())
        {
            foreach (var faction in prototype.NpcFactions)
            {
                _companyNpcFactions.Add(faction);
            }
        }

        return _companyNpcFactions;
    }

    private HashSet<ProtoId<NpcFactionPrototype>> GetTargetNpcFactions(string? companyId)
    {
        if (string.IsNullOrWhiteSpace(companyId) || companyId == "None")
            return new();

        if (!_prototypeManager.TryIndex<CompanyPrototype>(companyId, out var prototype))
            return new();

        return prototype.NpcFactions.Count == 0 ? new() : new HashSet<ProtoId<NpcFactionPrototype>>(prototype.NpcFactions);
    }

    private void OnExamined(EntityUid uid, Shared._Mono.Company.CompanyComponent component, ExaminedEvent args)
    {
        if (component.CompanyName == "None")
            return;

        var companyMarkup = GetVisibleCompanyMarkup(uid, args.Examiner, component);
        args.PushMarkup(Loc.GetString("examine-company",
            ("entity", uid),
            ("company", companyMarkup)),
            priority: 100);
    }

    private bool IsCompanyRevealedTo(EntityUid target, EntityUid examiner)
    {
        if (!TryComp<ActorComponent>(examiner, out var actor))
            return false;

        return TryComp<CompanyRevealComponent>(target, out var revealComp)
            && revealComp.RevealedToPlayerIds.Contains(actor.PlayerSession.UserId.ToString());
    }

    private bool IsCompanyPubliclyKnown(string companyId)
    {
        return _prototypeManager.TryIndex<CompanyPrototype>(companyId, out var prototype)
               && string.Equals(prototype.Form, "Протогонисты", StringComparison.OrdinalIgnoreCase);
    }

    private string GetCompanyId(EntityUid uid)
    {
        if (!TryComp<CompanyComponent>(uid, out var companyComp) || string.IsNullOrWhiteSpace(companyComp.CompanyName))
            return "None";

        return companyComp.CompanyName;
    }
}
