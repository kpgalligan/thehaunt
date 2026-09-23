using System.Text;
using Godot;

namespace TheHaunt.World;

/// <summary>
/// One line of the world dump (<see cref="WorldDump"/>): its sort key (y, x, kind, id)
/// and its fields, in write order. Values are what <see cref="WorldDump.Value"/> can
/// write: null, string, bool, int, float, Color, a nested field array, or a list.
/// </summary>
internal readonly record struct WorldDumpEntry(int Y, int X, string Kind, string Id, (string Key, object? Value)[] Fields)
{
    /// <summary>The entry's x/y/w/h rect, when it has one.</summary>
    public Rect2I? Rect =>
        Get("x") is int x && Get("y") is int y && Get("w") is int w && Get("h") is int h
            ? new Rect2I(x, y, w, h)
            : null;

    public object? Get(string key)
    {
        foreach ((string k, object? v) in Fields)
        {
            if (k == key)
                return v;
        }
        return null;
    }

    /// <summary>A copy with <paramref name="key"/> set: replaced in place if present, else appended.</summary>
    public WorldDumpEntry With(string key, object? value)
    {
        var fields = Fields.ToList();
        int index = fields.FindIndex(f => f.Key == key);
        if (index >= 0)
            fields[index] = (key, value);
        else
            fields.Add((key, value));
        return this with { Fields = fields.ToArray() };
    }

    public string Json()
    {
        var sb = new StringBuilder();
        WorldDump.Value(sb, Fields);
        return sb.ToString();
    }
}
