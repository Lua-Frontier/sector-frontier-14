// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

namespace Content.Server._Lua.Sectors;

public sealed class SectorBootstrapException : Exception
{
    public SectorBootstrapException(string message) : base(message)
    {
    }
}
