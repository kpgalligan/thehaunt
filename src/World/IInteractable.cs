using Godot;

namespace TheHaunt.World;

public interface IInteractable
{
    string PromptText { get; }
    bool CanInteract(Node2D interactor);
    void Interact(Node2D interactor);
}
