// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

namespace Content.Lua.Server.MaterialAmmo;

[RegisterComponent]
public sealed partial class MaterialAmmoComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<string>? AllowedMaterials;
}
