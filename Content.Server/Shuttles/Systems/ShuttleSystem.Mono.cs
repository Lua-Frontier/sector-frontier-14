using Content.Server.Shuttles.Components;

namespace Content.Server.Shuttles.Systems;

// shuttle impact damage ported from Goobstation (AGPLv3) with agreement of all coders involved
public sealed partial class ShuttleSystem
{
    /// <summary>
    /// Recursively gets all docked shuttles to the target shuttle.
    /// </summary>
    public void GetAllDockedShuttles(EntityUid shuttleUid, HashSet<EntityUid> dockedShuttles)
    {
        if (!dockedShuttles.Add(shuttleUid))
            return;  // Already processed this shuttle

        var docks = _dockSystem.GetDocks(shuttleUid);
        foreach (var dock in docks)
        {
            if (!TryComp<DockingComponent>(dock, out var dockComp) || dockComp.Docked == false)
                continue;
            if (dockComp.DockedWith == null)
                continue;
            var dockedGridUid = _transform.GetParentUid(dockComp.DockedWith.Value);
            if (dockedGridUid == EntityUid.Invalid || !_gridAccess.HasFtlGrid(dockedGridUid))
                continue;

            // If the docked shuttle has no FTLLockComponent or has it but it's disabled, skip adding it
            // to the FTL travel group, but still check its connections for potential conflicts
            if (!TryComp<FTLLockComponent>(dockedGridUid, out var ftlLock) || !ftlLock.Enabled)
            {
                // Still check this shuttle's connections without adding it to dockedShuttles
                var nestedDocks = _dockSystem.GetDocks(dockedGridUid);
                foreach (var nestedDock in nestedDocks)
                {
                    if (!TryComp<DockingComponent>(nestedDock, out var nestedDockComp) ||
                        nestedDockComp.Docked == false ||
                        nestedDockComp.DockedWith == null)
                        continue;

                    var nestedDockedGridUid = _transform.GetParentUid(nestedDockComp.DockedWith.Value);
                    // Skip the original grid and any invalid grids
                    if (nestedDockedGridUid == EntityUid.Invalid ||
                        nestedDockedGridUid == shuttleUid ||
                        !_gridAccess.HasFtlGrid(nestedDockedGridUid))
                        continue;

                    // Check if this grid should be added to the FTL travel group
                    if (TryComp<FTLLockComponent>(nestedDockedGridUid, out var nestedFtlLock) && nestedFtlLock.Enabled)
                    {
                        GetAllDockedShuttles(nestedDockedGridUid, dockedShuttles);
                    }
                }
                continue;
            }

            // If we haven't processed this grid yet, recursively get its docked shuttles
            if (!dockedShuttles.Contains(dockedGridUid))
            {
                GetAllDockedShuttles(dockedGridUid, dockedShuttles);
            }
        }
    }
}
