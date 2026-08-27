using Godot;
using FileAccess = Godot.FileAccess;

namespace TheHaunt.World;

/// <summary>
/// Where recipes live and how they get on and off disk: <c>res://data/maps/(mapId).json</c>.
///
/// Godot's <see cref="FileAccess"/> rather than System.IO, because res:// is a real
/// directory only while the editor is open — in an exported game it is inside the .pck
/// and nothing but FileAccess can read it. The same asymmetry is why writing is an
/// EDITOR-ONLY operation: recipes are content, read at map build time and written by the
/// placement tool, never by the running game.
///
/// No atomic tmp-and-rename ceremony here, deliberately. SaveService needs it because a
/// half-written save is a player's lost day; a half-written recipe is a source file the
/// author is looking at, in a directory under version control.
/// </summary>
public static class MapRecipeFile
{
    public const string Folder = "res://data/maps/";

    /// <summary>
    /// The farm's file is <c>test_farm.json</c>, because <c>MapIds.Farm</c> is literally
    /// "test_farm" — the rename is deferred to the first editor-authored map and its own
    /// migration, and paying it here would buy a mismatch between the id in the save and
    /// the name on disk. Deliberate; see MapIds.
    /// </summary>
    public static string PathFor(string mapId) => $"{Folder}{mapId}.json";

    /// <summary>
    /// The recipe for a map, or an EMPTY one if it has no file yet — a map without a
    /// recipe builds from its code defaults, which is every map today and is how a new
    /// map ships before anyone has dragged anything into it. A file that exists but does
    /// not parse throws (<see cref="MapRecipeException"/>): silence there would mean a
    /// typo quietly deletes half a map.
    /// </summary>
    public static MapRecipe Load(string mapId)
    {
        string path = PathFor(mapId);
        if (!FileAccess.FileExists(path))
        {
            return new MapRecipe(mapId);
        }

        return ReadFrom(path, mapId);
    }

    /// <summary>
    /// Reads any path (res:// or user://). Throws if it cannot be opened, cannot be
    /// parsed, or names a map other than <paramref name="expectedMapId"/> — a file whose
    /// header disagrees with its name is nearly always a copy that never got its header
    /// changed, and it would build the wrong map's furniture.
    /// </summary>
    public static MapRecipe ReadFrom(string path, string? expectedMapId = null)
    {
        using FileAccess? file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            throw new MapRecipeException(path, $"could not be opened ({FileAccess.GetOpenError()}).");
        }

        MapRecipe recipe = MapRecipe.Parse(file.GetAsText(), path);
        if (expectedMapId != null && recipe.MapId != expectedMapId)
        {
            throw new MapRecipeException(path,
                $"builds map '{recipe.MapId}', but it is filed under '{expectedMapId}'.");
        }
        return recipe;
    }

    /// <summary>Writes the canonical text to the map's own path. Editor-only — res:// is read-only in an exported game.</summary>
    public static void Save(MapRecipe recipe) => WriteTo(recipe, PathFor(recipe.MapId));

    /// <summary>Writes the canonical text to any path, creating the directory if it is missing.</summary>
    public static void WriteTo(MapRecipe recipe, string path)
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
        file.StoreString(recipe.ToJson());
        // Explicit: the handle closes when the object is freed, and "when" is not a thing
        // to leave to the GC while the next line may want to read the file back.
        file.Close();
    }
}
