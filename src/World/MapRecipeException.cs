namespace TheHaunt.World;

/// <summary>
/// A recipe that exists but cannot be read as one. Always names the file: a recipe is
/// read while a map is being built, so the stack it throws through says "map failed to
/// load" and nothing else — the path is the only part of the message that tells anyone
/// which file to open.
///
/// A MISSING recipe is not this: a map with no recipe yet builds from its code defaults.
/// </summary>
public sealed class MapRecipeException : Exception
{
    /// <summary>The file (or label) the problem is in.</summary>
    public string FilePath { get; }

    /// <param name="problem">Reads on from the path: "is not valid JSON", "placement 3 has no 'kind'".</param>
    public MapRecipeException(string filePath, string problem)
        : base($"Map recipe '{filePath}' {problem}")
    {
        FilePath = filePath;
    }
}
