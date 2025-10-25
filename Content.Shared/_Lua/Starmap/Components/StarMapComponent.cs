// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

namespace Content.Shared._Lua.Starmap.Components;

[RegisterComponent]
public sealed partial class StarMapComponent : Component
{
    [ViewVariables]
    public List<Star> StarMap = new();

    public void AddStar(Star star)
    { StarMap.Add(star); }

    public bool RemoveStarByName(string name)
    {
        var initialCount = StarMap.Count;
        StarMap.RemoveAll(star => star.Name == name);
        return StarMap.Count < initialCount;
    }

    public void ClearStars()
    { StarMap.Clear(); }

    public void UpdateStars(List<Star> newStars)
    {
        StarMap.Clear();
        StarMap.AddRange(newStars);
    }
}
