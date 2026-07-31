using System.Numerics;
using System.Threading.Tasks;
using Content.Shared.Maps;
using Content.Shared.Procedural;
using Content.Shared.Procedural.PostGeneration;
using Robust.Shared.Map;

namespace Content.Server.Procedural.DungeonJob;

public sealed partial class DungeonJob
{
    /// <summary>
    /// <see cref="CorridorDunGen"/>
    /// </summary>
    private async Task PostGen(CorridorDunGen gen, Dungeon dungeon, HashSet<Vector2i> reservedTiles, Random random)
    {
        var entrances = new List<Vector2i>(dungeon.Rooms.Count);

        // Grab entrances
        foreach (var room in dungeon.Rooms)
        {
            entrances.AddRange(room.Entrances);
        }

        if (entrances.Count == 0) return;
        var edges = _dungeon.MinimumSpanningTree(entrances, random);
        await SuspendDungeon();

        if (!ValidateResume())
            return;

        // TODO: Add in say 1/3 of edges back in to add some cyclic to it.

        var expansion = gen.Width - 2;
        // Okay so tl;dr is that we don't want to cut close to rooms as it might go from 3 width to 2 width suddenly
        // So we will add a buffer range around each room to deter pathfinding there unless necessary
        var deterredTiles = new HashSet<Vector2i>();

        if (expansion >= 1)
        {
            foreach (var tile in dungeon.RoomExteriorTiles)
            {
                for (var x = -expansion; x <= expansion; x++)
                {
                    for (var y = -expansion; y <= expansion; y++)
                    {
                        var neighbor = new Vector2(tile.X + x, tile.Y + y).Floored();

                        if (dungeon.RoomTiles.Contains(neighbor) ||
                            dungeon.RoomExteriorTiles.Contains(neighbor) ||
                            entrances.Contains(neighbor))
                        {
                            continue;
                        }

                        deterredTiles.Add(neighbor);
                    }
                }
            }
        }

        foreach (var room in dungeon.Rooms)
        {
            foreach (var entrance in room.Entrances)
            {
                // Just so we can still actually get in to the entrance we won't deter from a tile away from it.
                var normal = (entrance + _grid.TileSizeHalfVector - room.Center).ToWorldAngle().GetCardinalDir().ToIntVec();
                deterredTiles.Remove(entrance + normal);
            }
        }

        var excludedTiles = new HashSet<Vector2i>(dungeon.RoomExteriorTiles);
        excludedTiles.UnionWith(dungeon.RoomTiles);
        var corridorTiles = new HashSet<Vector2i>();

        _dungeon.GetCorridorNodes(corridorTiles, edges, gen.PathLimit, excludedTiles, tile =>
        {
            var mod = 1f;

            if (corridorTiles.Contains(tile))
            {
                mod *= 0.1f;
            }

            if (deterredTiles.Contains(tile))
            {
                mod *= 2f;
            }

            return mod;
        }, dungeon.RoomTiles);

        WidenCorridor(dungeon, gen.Width, corridorTiles);

        var punched = EnsureRoomConnectivity(dungeon, corridorTiles, reservedTiles);
        var setTiles = new List<(Vector2i, Tile)>();
        var tileDef = (ContentTileDefinition) _tileDefManager[gen.Tile];

        foreach (var tile in corridorTiles)
        {
            if (reservedTiles.Contains(tile))
                continue;

            setTiles.Add((tile, _tile.GetVariantTile(tileDef, random)));
        }

        _maps.SetTiles(_gridUid, _grid, setTiles);
        dungeon.CorridorTiles.UnionWith(corridorTiles);
        foreach (var tile in punched)
        { ClearTileBlockers(tile); }
        foreach (var entrance in dungeon.Entrances)
        {
            ClearTileBlockers(entrance);
            ClearDoor(dungeon, _grid, entrance);
        }

        dungeon.RefreshAllTiles();
        BuildCorridorExterior(dungeon);
    }

    private HashSet<Vector2i> EnsureRoomConnectivity(Dungeon dungeon, HashSet<Vector2i> corridorTiles, HashSet<Vector2i> reservedTiles)
    {
        var punched = new HashSet<Vector2i>();
        if (dungeon.Rooms.Count <= 1) return punched;
        var walkable = new HashSet<Vector2i>(dungeon.RoomTiles);
        walkable.UnionWith(corridorTiles);
        walkable.UnionWith(dungeon.Entrances);
        var reachable = FloodWalkable(walkable, dungeon.Rooms[0].Tiles);
        if (reachable.Count == 0) return punched;
        for (var i = 1; i < dungeon.Rooms.Count; i++)
        {
            var room = dungeon.Rooms[i];
            var connected = false;
            foreach (var tile in room.Tiles)
            {
                if (reachable.Contains(tile))
                {
                    connected = true;
                    break;
                }
            }
            if (connected) continue;
            var start = PickRoomPortal(room);
            var target = FindNearestReachable(start, reachable);
            if (target == null) continue;
            foreach (var tile in BuildLPath(start, target.Value))
            {
                if (reservedTiles.Contains(tile)) continue;
                if (dungeon.RoomTiles.Contains(tile) && !room.Tiles.Contains(tile)) continue;
                if (corridorTiles.Add(tile)) punched.Add(tile);
                walkable.Add(tile);
                if (room.Exterior.Contains(tile) || room.Entrances.Contains(tile))
                {
                    if (!room.Entrances.Contains(tile)) room.Entrances.Add(tile);
                    dungeon.Entrances.Add(tile);
                    punched.Add(tile);
                }
            }
            foreach (var tile in room.Tiles)
            { walkable.Add(tile); }
            var grown = FloodWalkable(walkable, room.Tiles);
            reachable.UnionWith(grown);
        }
        return punched;
    }

    private static HashSet<Vector2i> FloodWalkable(HashSet<Vector2i> walkable, HashSet<Vector2i> seeds)
    {
        var reachable = new HashSet<Vector2i>();
        var queue = new Queue<Vector2i>();
        foreach (var seed in seeds)
        {
            if (!walkable.Contains(seed) || !reachable.Add(seed)) continue;
            queue.Enqueue(seed);
        }
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            TryEnqueue(node + new Vector2i(1, 0));
            TryEnqueue(node + new Vector2i(-1, 0));
            TryEnqueue(node + new Vector2i(0, 1));
            TryEnqueue(node + new Vector2i(0, -1));
            void TryEnqueue(Vector2i nb)
            {
                if (!walkable.Contains(nb) || !reachable.Add(nb)) return;
                queue.Enqueue(nb);
            }
        }
        return reachable;
    }

    private static Vector2i PickRoomPortal(DungeonRoom room)
    {
        if (room.Entrances.Count > 0) return room.Entrances[0];
        return new Vector2i(room.Bounds.Left + room.Bounds.Width / 2, room.Bounds.Bottom - 1);
    }

    private static Vector2i? FindNearestReachable(Vector2i from, HashSet<Vector2i> reachable)
    {
        Vector2i? best = null;
        var bestDist = int.MaxValue;
        foreach (var tile in reachable)
        {
            var dist = Math.Abs(tile.X - from.X) + Math.Abs(tile.Y - from.Y);
            if (dist >= bestDist) continue;
            bestDist = dist;
            best = tile;
        }
        return best;
    }

    private static List<Vector2i> BuildLPath(Vector2i from, Vector2i to)
    {
        var path = new List<Vector2i>();
        var pos = from;
        var corner = new Vector2i(to.X, from.Y);
        while (pos != corner)
        {
            pos = pos.X != corner.X ? new Vector2i(pos.X + Math.Sign(corner.X - pos.X), pos.Y) : new Vector2i(pos.X, pos.Y + Math.Sign(corner.Y - pos.Y));
            path.Add(pos);
        }
        while (pos != to)
        {
            pos = pos.X != to.X ? new Vector2i(pos.X + Math.Sign(to.X - pos.X), pos.Y) : new Vector2i(pos.X, pos.Y + Math.Sign(to.Y - pos.Y));
            path.Add(pos);
        }
        return path;
    }
}
