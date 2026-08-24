namespace TheHaunt.Core;

// Node-owned volatile state only (player position/facing is essentially the whole list).
// If this registry grows past ~the player, state is leaking into the scene tree.
public interface IPersistentSystem
{
    void WriteState(GameData data);
    void ReadState(GameData data);
}
