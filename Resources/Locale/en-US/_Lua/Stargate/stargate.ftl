stargate-console-title = Dialing device
stargate-console-dial = Dial
stargate-console-clear = Clear
stargate-console-close-portal = Close Portal
stargate-console-copy = Copy
stargate-console-gate-address-label = Current gate address
stargate-console-status-idle = Ready
stargate-console-status-input = Entering address...
stargate-console-status-dialing = Establishing connection...
stargate-console-status-active = Wormhole Active
stargate-console-no-gate = No linked Stargate found.
stargate-console-invalid-address = Invalid address.
stargate-console-dial-failed = Failed to establish connection.
stargate-console-overloaded = Gate system overloaded
stargate-console-gate-busy = Stargate is busy — incoming wormhole active.
stargate-controllable-activate = Activate Stargate
stargate-controllable-deactivate = Deactivate Stargate
stargate-controllable-activated = Stargate activated.
stargate-controllable-deactivated = Stargate deactivated.
stargate-console-iris-lock = Close iris
stargate-console-iris-unlock = Open iris
stargate-console-iris-closed = Gate iris is closed.
stargate-console-disk-title = Address Disk
stargate-console-disk-save = Save Address
stargate-console-disk-dial = Dial
stargate-console-status-auto-dialing = Auto-dialing...

stargate-editor-title = Stargate data console
stargate-editor-disk-left = Left Disk
stargate-editor-disk-right = Right Disk
stargate-editor-save-left = Save to Left
stargate-editor-save-right = Save to Right
stargate-editor-clone-to-right = Clone all to Right
stargate-editor-clone-to-left = Clone all to Left

stargate-minimap-title = Cartographer
stargate-minimap-disk-active = Disk: Active
stargate-minimap-disk-empty = Disk: Empty
stargate-minimap-disk-ready = Disk: Ready
stargate-minimap-merge-1-to-2 = 1 → 2
stargate-minimap-merge-2-to-1 = 2 → 1
stargate-minimap-status = Cartographer Lua Technologies
stargate-minimap-not-planet = Not a planet
stargate-minimap-insert-disk = Insert cartography disk
stargate-minimap-gate = Gate

ent-StargateConsole = dialing device
    .desc = Control panel for dialing Stargate addresses and establishing wormhole connections.

ent-StargateAddressEditorConsole = stargate data console
    .desc = Workstation for managing Stargate address disks. Copy and edit coordinates between two disks. Does not dial gates.

ent-StargateControllableLuaTech = stargate
    .desc = A Stargate that can be manually activated or deactivated.
ent-StargateControllableSyndicate = { ent-Stargate }
    .desc = { ent-Stargate }
    .suffix = Syndicate
ent-Stargate = stargate
    .desc = An ancient ring-shaped device capable of creating stable wormholes to distant worlds.

ent-StargateAddressDisk = stargate coordinates disk
    .desc = Encrypted data disk containing Stargate coordinates. Insert into dialing device.

ent-StargateDebugPaper = random stargate addresses
    .desc = Document with random addresses. DO NOT MAP.
ent-StargateAddressPaper = worn paper with symbols
    .desc = A scrap of paper with an address scrawled on it.

ent-SpawnStargateAddressPaper = stargate address paper spawner
    .desc = Spawns a paper with a random Stargate address. For map editing; purple marker + paper sprite for positioning.

ent-StargateMinimapTablet = cartographic tablet
    .desc = A handheld tablet for mapping worlds. Lua Technologies engraving on the back. Some say Lua Technologies gathers intel on worlds through them.

ent-StargateMinimapDisk = cartography disk
    .desc = A data disk for storing world cartographic data. Insert into a map Tablet.
