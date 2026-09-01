namespace TheHaunt.Core;

/// <summary>
/// A quest is a NAMED WINDOW BETWEEN TWO STORY FLAGS — the model keeps no quest
/// objects at all. StartFlag stamps when something (a conversation's SetsFlag, a
/// letter's ReadFlag) hands the quest out; CompleteFlag stamps when the world event
/// it asks for happens, whether or not the quest was ever started. QuestRules
/// derives the lifecycle from the two stamps; both must be StoryKeys constants
/// (validation-tested), and they carry their day-indexes for free.
/// </summary>
public sealed record QuestDef(
    string Id,
    string Title,
    string Description,
    string StartFlag,
    string CompleteFlag);
