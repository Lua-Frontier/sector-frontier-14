using Content.Server.Popups;
using Content.Shared._Lua.FtlPoints;
using Content.Shared._Lua.FtlPoints.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;

namespace Content.Server._Lua.FTLPoints.Systems;

public sealed partial class FtlPointsSystem : SharedFtlPointsSystem
{
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SectorStarMapSystem _sectorStarMap = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StarmapConsoleComponent, AfterActivatableUIOpenEvent>(OnToggleInterface);
        SubscribeLocalEvent<WarpDriveComponent, InteractHandEvent>(OnDriveInteractHand);
        SubscribeLocalEvent<WarpDriveComponent, ExaminedEvent>(OnDriveExamineEvent);
    }

    private void OnToggleInterface(EntityUid uid, StarmapConsoleComponent component, AfterActivatableUIOpenEvent args)
    {
        if (!TryComp<StarMapComponent>(uid, out var starMap)) return;
        try
        {
            if (_sectorStarMap != null)
            {
                Log.Info("Updating StarMap before opening interface...");
                _sectorStarMap.UpdateAllStarMaps();
            }
            else
            { Log.Warning("SectorStarMapSystem is not available"); }
        }
        catch (Exception ex)
        { Log.Error($"Error updating StarMap: {ex}"); }
        var allStars = GetAllStars();
        var state = new StarmapConsoleBoundUserInterfaceState(allStars, 100f);
        _userInterface.SetUiState(uid, StarmapConsoleUiKey.Key, state);
        Log.Info($"Interface opened with {allStars.Count} total stars");
    }

    private List<Star> GetAllStars()
    {
        var stars = new List<Star>();
        var starMapQuery = AllEntityQuery<StarMapComponent>();
        while (starMapQuery.MoveNext(out var uid, out var starMap))
        {
            stars.AddRange(starMap.StarMap);
        }
        try
        {
            if (_sectorStarMap != null)
            {
                var sectorStars = _sectorStarMap.GetSectorStars();
                stars.AddRange(sectorStars);
                Log.Info($"Added {sectorStars.Count} sector stars to StarMap");
            }
            else
            { Log.Warning("SectorStarMapSystem is not available"); }
        }
        catch (Exception ex)
        { Log.Error($"Error getting sector stars: {ex}"); }
        return stars;
    }

    private void OnDriveInteractHand(EntityUid uid, WarpDriveComponent component, InteractHandEvent args)
    {
        if (component.Charging)
        {
            component.Charging = false;
            _popupSystem.PopupEntity(Loc.GetString("popup-drive-not-charging"), args.User, args.User);
        }
        else
        {
            component.Charging = true;
            _popupSystem.PopupEntity(Loc.GetString("popup-drive-charging"), args.User, args.User);
        }
    }

    private void OnDriveExamineEvent(EntityUid uid, WarpDriveComponent component, ExaminedEvent args)
    {
        var charging = component.Charging ? "charging" : "not charging";
        var charge = component.Charge;
        var destination = component.Charge >= component.ChargeNeeded ? "Destination set." : "No destination set.";
        args.PushMarkup(Loc.GetString("drive-examined",
            ("charging", charging),
            ("charge", charge),
            ("destination", destination)));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        DriveUpdate(frameTime);
    }

    private void DriveUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<WarpDriveComponent>();

        while (query.MoveNext(out var uid, out var drive))
        {
            if (!drive.Charging) continue;
            drive.Charge += frameTime * 10;
            if (drive.Charge >= drive.ChargeNeeded)
            {
                drive.Charge = drive.ChargeNeeded;
                drive.Charging = false;
            }
        }
    }
}
