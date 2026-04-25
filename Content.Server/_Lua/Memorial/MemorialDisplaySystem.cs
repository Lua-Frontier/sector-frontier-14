// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.Memorial;
using Robust.Server.GameObjects;

namespace Content.Server._Lua.Memorial;

public sealed class MemorialDisplaySystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<MemorialDisplayComponent>(MemorialDisplayUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
        });
    }

    private void OnUiOpened(Entity<MemorialDisplayComponent> ent, ref BoundUIOpenedEvent args)
    {
        var state = BuildUiState(ent.Comp);
        _ui.SetUiState(ent.Owner, MemorialDisplayUiKey.Key, state);
    }

    private static MemorialDisplayUiState BuildUiState(MemorialDisplayComponent component)
    {
        var entries = new List<string>(component.Entries.Count);

        foreach (var entry in component.Entries)
        {
            if (!string.IsNullOrWhiteSpace(entry.Markup))
            {
                entries.Add(entry.Markup);
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.Description))
                entries.Add($"[bold]{entry.Nickname}[/bold]");
            else
                entries.Add($"[bold]{entry.Nickname}[/bold] - {entry.Description}");
        }

        return new MemorialDisplayUiState(component.DisplayName, component.DisplayDescription, entries);
    }
}
