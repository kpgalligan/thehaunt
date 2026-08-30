namespace TheHaunt.Core;

/// <summary>One garage job resolved at dawn, for the overnight report: a paid
/// completion (Proceeds = the service's price) or a reclaimed unfinished car
/// (Reclaimed, Proceeds 0 — the customer took it back and paid nothing).</summary>
public readonly record struct GarageLine(string ServiceId, long Proceeds, bool Reclaimed);
