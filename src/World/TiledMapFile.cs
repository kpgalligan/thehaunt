using Godot;
using FileAccess = Godot.FileAccess;

namespace TheHaunt.World;

/// <summary>
/// Where Tiled maps live and how they get on and off disk: <c>res://data/maps/(mapId).tmx</c>,
/// with Tiled's support files (the surface palette, the project) under
/// <c>res://data/maps/tiled/</c>. Godot's <see cref="FileAccess"/>, because res:// is
/// inside the .pck in an exported game.
/// Writing is dev/editor-only — the running game never writes content.
///
/// A DEV FILE (<see cref="UseDevFile"/>, the <c>--tiled-file</c> flag) stands in for a
/// map's shipped file in <see cref="Load"/> only — for checking a painted copy without
/// touching the shipped one. <see cref="Exists"/> and <see cref="TiledSeeds"/> never
/// consult it.
/// </summary>
public static class TiledMapFile
{
    public const string Folder = "res://data/maps/";
    public const string SupportFolder = "res://data/maps/tiled/";

    // Map id -> dev file path. Empty in the running game unless --tiled-file was passed.
    private static readonly Dictionary<string, string> DevFiles = new(StringComparer.Ordinal);

    public static string PathFor(string mapId) => $"{Folder}{mapId}.tmx";

    /// <summary>
    /// Reads <paramref name="path"/> (throwing <see cref="MapRecipeException"/> if it
    /// cannot) and registers it as the dev file for the map its <c>map</c> property names,
    /// so <see cref="Load"/> reads it instead of the shipped file. Dev-only.
    /// </summary>
    public static void UseDevFile(string path)
    {
        TiledMap map = ReadFrom(path);
        DevFiles[map.MapId] = path;
    }

    /// <summary>Forgets every dev file (tests).</summary>
    internal static void ClearDevFiles() => DevFiles.Clear();

    public static bool Exists(string mapId) => FileAccess.FileExists(PathFor(mapId));

    /// <summary>
    /// The map's Tiled file — a registered dev file first, else the shipped one — or null
    /// when it has none: a missing file means the map builds from its code defaults. A
    /// file that exists but cannot be read throws (<see cref="MapRecipeException"/>).
    /// </summary>
    public static TiledMap? Load(string mapId) =>
        DevFiles.TryGetValue(mapId, out string? dev) ? ReadFrom(dev, mapId)
        : Exists(mapId) ? ReadFrom(PathFor(mapId), mapId)
        : null;

    /// <summary>
    /// Reads any path. Throws if it cannot be opened or parsed, or names a map other than
    /// <paramref name="expectedMapId"/> — a copy that never got its header changed would
    /// build the wrong map.
    /// </summary>
    public static TiledMap ReadFrom(string path, string? expectedMapId = null)
    {
        using FileAccess? file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            throw new MapRecipeException(path, $"could not be opened ({FileAccess.GetOpenError()}).");
        }

        TiledMap map = TiledMap.Parse(file.GetAsText(), path);
        if (expectedMapId != null && map.MapId != expectedMapId)
        {
            throw new MapRecipeException(path,
                $"builds map '{map.MapId}', but it is filed under '{expectedMapId}'.");
        }
        return map;
    }

    /// <summary>Writes text to a path, creating its directory. Dev/editor-only.</summary>
    public static void WriteText(string path, string text)
    {
        string folder = path[..(path.LastIndexOf('/') + 1)];
        if (folder.Length > 0 && !DirAccess.DirExistsAbsolute(folder))
        {
            Error error = DirAccess.MakeDirRecursiveAbsolute(folder);
            if (error != Error.Ok)
            {
                throw new MapRecipeException(path, $"has no directory to live in ({error}).");
            }
        }

        using FileAccess? file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            throw new MapRecipeException(path, $"could not be opened for writing ({FileAccess.GetOpenError()}).");
        }
        file.StoreString(text);
        file.Close();
    }
}
