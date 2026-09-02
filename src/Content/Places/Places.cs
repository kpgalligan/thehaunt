using System.Reflection;

namespace TheHaunt.Content;

/// <summary>
/// The place files, enumerable — what the content dump and the copy tests walk.
/// Order is geographic: the farm and its barn, then the road west to east. A place's
/// COPY is its public const strings (by convention <c>...Text</c> for boards the
/// player reads, <c>...Line</c> for the answers a handle or fixture speaks);
/// <see cref="CopyOf"/> reflects them, so a new const is swept and dumped with
/// nothing to register.
/// </summary>
public static class Places
{
    public static IReadOnlyList<Type> All { get; } = new[]
    {
        typeof(Farm), typeof(BarnRules),
        typeof(MotelRules), typeof(GasStation), typeof(FireworksStand), typeof(Garage),
        typeof(Billies), typeof(Fork), typeof(Town),
        typeof(EastFork), typeof(DriveIn), typeof(EastEntry),
    };

    /// <summary>Every public const string on a place class except MapId — its copy.</summary>
    public static IEnumerable<(string Name, string Value)> CopyOf(Type place)
    {
        foreach (FieldInfo field in place.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.IsLiteral && field.FieldType == typeof(string) && field.Name != "MapId")
            {
                yield return (field.Name, (string)field.GetRawConstantValue()!);
            }
        }
    }
}
