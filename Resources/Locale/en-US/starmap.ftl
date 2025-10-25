# Starmap Console
starmap-computer-title = Starmap Console

# Starmap Details Display
starmap-details-display-label = General Details
starmap-star-details-current-star = Current Star:
starmap-star-details-spin-range = Spin Range:
starmap-crystal-integrity = Crystal Integrity:

# Star Details Display
starmap-star-details-display-label = Star Details
starmap-star-details-name = Name:
starmap-star-details-coordinates = Coordinates:
starmap-star-details-button-warp = Warp

# Star Details Position
starmap-star-details-position = X: { $x }, Y: { $y }

# Center Station
starmap-center-station = Center Station

# Starmap UI
starmap-ui-title = Star Map
starmap-refresh = Refresh
starmap-warp-status = Warp status
starmap-warp-ready = Available
starmap-warp-seconds = { $seconds }s
starmap-warp-cooldown = Warp recharging: { $seconds }s
starmap-warp-cooldown-time = Warp recharging: { $minutes }m { $seconds }s
starmap-already-here = Already in this sector
starmap-no-hyperlane = Cannot warp: no hyperlane to the target
starmap-no-warpdrive = No warp drive installed on the ship

# Ship FTL Tags
ship-ftl-tag-base = [BASE]
ship-ftl-tag-star = [STAR]
ship-ftl-tag-planet = [PLANET]
ship-ftl-tag-asteroid = [ASTEROID]
ship-ftl-tag-ruin = [RUIN]
ship-ftl-tag-warp = [WARP]
ship-ftl-tag-oor = Out of Range

# Drive Messages
popup-drive-charging = Drive is now charging
popup-drive-not-charging = Drive is no longer charging

# Drive Examination
drive-examined-multiple-drives = Multiple drives detected on this grid. Only one drive per grid is supported.
drive-examined-ready = Drive is ready for warp.
drive-examined = Drive is { $charging ->
    [true] charging
    *[false] not charging
} ({ $charge }% complete). { $destination ->
    [true] Destination set.
    *[false] No destination set.
}
