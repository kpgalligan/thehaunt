namespace TheHaunt.Core;

public sealed class MapState
{
    public List<TileRecord> Tiles { get; set; } = new();
    public List<PlacedObjectRecord> Objects { get; set; } = new();

    // Runtime index: packed coord -> position in Tiles. Not serialized; rebuilt on load.
    private readonly Dictionary<long, int> _index = new();

    private static long Pack(int x, int y) => ((long)y << 32) | (uint)x;

    public TileRecord? GetTile(int x, int y) =>
        _index.TryGetValue(Pack(x, y), out var i) ? Tiles[i] : null;

    // Upsert by (X, Y), maintains the index.
    public void SetTile(TileRecord record)
    {
        long key = Pack(record.X, record.Y);
        if (_index.TryGetValue(key, out var i))
        {
            Tiles[i] = record;
        }
        else
        {
            _index[key] = Tiles.Count;
            Tiles.Add(record);
        }
    }

    // Swap-remove, fixes the index; true if a tile was removed.
    public bool RemoveTile(int x, int y)
    {
        long key = Pack(x, y);
        if (!_index.TryGetValue(key, out var i))
        {
            return false;
        }
        int last = Tiles.Count - 1;
        if (i != last)
        {
            var moved = Tiles[last];
            Tiles[i] = moved;
            _index[Pack(moved.X, moved.Y)] = i;
        }
        Tiles.RemoveAt(last);
        _index.Remove(key);
        return true;
    }

    // Call after deserialization. Compacts duplicate (X, Y) records (last wins) so the
    // 1:1 Tiles<->index invariant that SetTile/RemoveTile rely on holds even for
    // hand-edited or corrupted files.
    public void RebuildIndex()
    {
        _index.Clear();
        var deduped = new List<TileRecord>(Tiles.Count);
        foreach (TileRecord tile in Tiles)
        {
            long key = Pack(tile.X, tile.Y);
            if (_index.TryGetValue(key, out var i))
            {
                deduped[i] = tile;
            }
            else
            {
                _index[key] = deduped.Count;
                deduped.Add(tile);
            }
        }
        Tiles = deduped;
    }
}
