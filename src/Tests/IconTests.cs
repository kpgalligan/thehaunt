using Godot;
using TheHaunt.Core;
using TheHaunt.UI;

namespace TheHaunt.Tests;

/// <summary>
/// Pins the item-icon atlas (assets/sprites/icons/items.png, tools/gen_item_icons.py)
/// to the item registry: every ItemDef has a drawn icon, in ItemDefs.All order, and
/// no icon is an accidental copy of another.
/// </summary>
public static class IconTests
{
    [SimTest]
    public static void Icons_AtlasMatchesTheItemRegistry(TestContext t)
    {
        t.AssertEqual(
            string.Join(",", ItemDefs.All.Keys),
            string.Join(",", ItemIcons.Order),
            "ItemIcons.Order mirrors ItemDefs.All exactly (add new items to BOTH " +
            "tables and rerun tools/gen_item_icons.py)");

        Image atlas = GD.Load<Texture2D>(ItemIcons.SheetPath).GetImage();
        t.AssertEqual(ItemIcons.Order.Count * ItemIcons.Size, atlas.GetWidth(), "atlas width");
        t.AssertEqual(ItemIcons.Size, atlas.GetHeight(), "atlas height");

        foreach (string id in ItemIcons.Order)
        {
            t.Assert(ItemIcons.For(id) is AtlasTexture, $"'{id}' resolves to an atlas region");
        }
        t.Assert(ItemIcons.For("future.artifact") is null,
            "unknown ids resolve to null — the caller's '?' placeholder path");
    }

    [SimTest]
    public static void Icons_AreDrawnAndDistinct(TestContext t)
    {
        Image atlas = GD.Load<Texture2D>(ItemIcons.SheetPath).GetImage();
        var seen = new Dictionary<string, string>();
        for (int column = 0; column < ItemIcons.Order.Count; column++)
        {
            string id = ItemIcons.Order[column];
            int drawn = 0;
            var pixels = new System.Text.StringBuilder();
            for (int y = 0; y < ItemIcons.Size; y++)
            {
                for (int x = 0; x < ItemIcons.Size; x++)
                {
                    Color pixel = atlas.GetPixel(column * ItemIcons.Size + x, y);
                    if (pixel.A > 0f)
                    {
                        drawn++;
                        string html = pixel.ToHtml(false);
                        t.Assert(html != "6b4560" && html != "7d8f4a",
                            $"'{id}' keeps the reserved dread accents off Act I art");
                        pixels.Append(html);
                    }
                    else
                    {
                        pixels.Append('.');
                    }
                }
            }
            t.Assert(drawn > 25, $"'{id}' is drawn ({drawn} px)");
            t.Assert(drawn < ItemIcons.Size * ItemIcons.Size, $"'{id}' is an icon, not a filled cube");
            string key = pixels.ToString();
            t.Assert(!seen.ContainsKey(key), $"'{id}' is not a pixel-for-pixel copy of '{seen.GetValueOrDefault(key)}'");
            seen[key] = id;
        }
    }
}
