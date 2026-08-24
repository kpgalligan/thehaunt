using Godot;

namespace TheHaunt.UI;

/// <summary>Full-screen black fade used by the sleep flow. Animates via a manual
/// frame loop rather than a Tween: a node-bound tween is killed silently when the node
/// is freed and its Finished signal never fires, which would hang any awaiting flow —
/// the loop instead throws on a freed node, so callers' finally blocks still run.
/// Keeps animating while the tree is paused.</summary>
public partial class ScreenFade : ColorRect
{
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(0f, 0f, 0f, 0f);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public async Task FadeOut(double seconds = 0.4)
    {
        await FadeTo(1f, seconds);
    }

    public async Task FadeIn(double seconds = 0.4)
    {
        await FadeTo(0f, seconds);
    }

    private async Task FadeTo(float targetAlpha, double seconds)
    {
        float startAlpha = Color.A;
        SceneTree tree = GetTree();
        double elapsed = 0;
        while (elapsed < seconds)
        {
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            elapsed += GetProcessDeltaTime();
            float weight = (float)Math.Clamp(elapsed / seconds, 0.0, 1.0);
            Color = new Color(0f, 0f, 0f, Mathf.Lerp(startAlpha, targetAlpha, weight));
        }
    }
}
