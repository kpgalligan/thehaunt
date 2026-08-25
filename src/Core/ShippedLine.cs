namespace TheHaunt.Core;

// One SOLD shipping-bin stack in an OvernightReport. Unknown or unsellable
// ids stay binned and produce no line (item deletion is data loss).
public readonly record struct ShippedLine(string ItemId, int Count, long Proceeds);
