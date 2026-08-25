using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.UI;

/// <summary>Bottom-center 10-slot hotbar mirroring the player's inventory. Pure view:
/// redraws all slots from the model on WorldSim.InventoryChanged / SaveService.AfterLoad.
/// Unknown item ids render as a gray '?' placeholder — never dropped, never thrown on.</summary>
public partial class HotbarUi : Control
{
    private const int SlotSize = 26;
    private const int IconSize = 12;

    private readonly Panel[] _slotPanels = new Panel[InventoryData.Capacity];
    private readonly TextureRect[] _slotIcons = new TextureRect[InventoryData.Capacity];
    private readonly Label[] _slotCounts = new Label[InventoryData.Capacity];

    private readonly Dictionary<string, ImageTexture> _iconCache = new();
    private ImageTexture? _placeholderIcon;

    private StyleBoxFlat _normalStyle = null!;
    private StyleBoxFlat _selectedStyle = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        _normalStyle = MakeSlotStyle(selected: false);
        _selectedStyle = MakeSlotStyle(selected: true);

        var hbox = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        hbox.AddThemeConstantOverride("separation", 2);
        hbox.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom, LayoutPresetMode.Minsize, margin: 8);
        hbox.GrowHorizontal = GrowDirection.Both;
        hbox.GrowVertical = GrowDirection.Begin;
        AddChild(hbox);

        for (int i = 0; i < InventoryData.Capacity; i++)
        {
            var panel = new Panel
            {
                CustomMinimumSize = new Vector2(SlotSize, SlotSize),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            panel.AddThemeStyleboxOverride("panel", _normalStyle);
            hbox.AddChild(panel);

            var icon = new TextureRect
            {
                StretchMode = TextureRect.StretchModeEnum.KeepCentered,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            panel.AddChild(icon);

            var count = new Label
            {
                Visible = false,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            count.AddThemeFontSizeOverride("font_size", 8);
            count.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
            count.AddThemeConstantOverride("outline_size", 2);
            count.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight, LayoutPresetMode.Minsize, margin: 1);
            count.GrowHorizontal = GrowDirection.Begin;
            count.GrowVertical = GrowDirection.Begin;
            panel.AddChild(count);

            _slotPanels[i] = panel;
            _slotIcons[i] = icon;
            _slotCounts[i] = count;
        }

        WorldSim.Instance.InventoryChanged += Redraw;
        SaveService.Instance.AfterLoad += Redraw;

        Redraw();
    }

    public override void _ExitTree()
    {
        WorldSim.Instance.InventoryChanged -= Redraw;
        SaveService.Instance.AfterLoad -= Redraw;
    }

    private void Redraw()
    {
        InventoryData inventory = SaveService.Instance.Current.Player.Inventory;
        for (int i = 0; i < InventoryData.Capacity; i++)
        {
            ItemStackRecord? stack = inventory.SlotAt(i);
            if (stack == null)
            {
                _slotIcons[i].Texture = null;
                _slotCounts[i].Visible = false;
            }
            else
            {
                _slotIcons[i].Texture = IconFor(stack.ItemId);
                _slotCounts[i].Text = stack.Count.ToString();
                _slotCounts[i].Visible = stack.Count > 1;
            }

            bool selected = i == inventory.SelectedSlot;
            _slotPanels[i].AddThemeStyleboxOverride("panel", selected ? _selectedStyle : _normalStyle);
        }
    }

    private ImageTexture IconFor(string itemId)
    {
        if (_iconCache.TryGetValue(itemId, out ImageTexture? cached))
        {
            return cached;
        }

        ItemDef? def = ItemDefs.TryGet(itemId);
        ImageTexture texture = def != null
            ? BuildItemIcon(def)
            : _placeholderIcon ??= BuildPlaceholderIcon();
        _iconCache[itemId] = texture;
        return texture;
    }

    private static ImageTexture BuildItemIcon(ItemDef def)
    {
        Color fill = Color.HtmlIsValid(def.IconColor)
            ? Color.FromHtml(def.IconColor)
            : new Color(0.5f, 0.5f, 0.5f);

        var img = Image.CreateEmpty(IconSize, IconSize, false, Image.Format.Rgba8);
        img.Fill(fill);
        Color edge = fill.Darkened(0.35f);
        for (int i = 0; i < IconSize; i++)
        {
            img.SetPixel(i, 0, edge);
            img.SetPixel(i, IconSize - 1, edge);
            img.SetPixel(0, i, edge);
            img.SetPixel(IconSize - 1, i, edge);
        }
        return ImageTexture.CreateFromImage(img);
    }

    private static ImageTexture BuildPlaceholderIcon()
    {
        var img = Image.CreateEmpty(IconSize, IconSize, false, Image.Format.Rgba8);
        img.Fill(new Color(0.42f, 0.42f, 0.42f));
        Color edge = new(0.28f, 0.28f, 0.28f);
        for (int i = 0; i < IconSize; i++)
        {
            img.SetPixel(i, 0, edge);
            img.SetPixel(i, IconSize - 1, edge);
            img.SetPixel(0, i, edge);
            img.SetPixel(IconSize - 1, i, edge);
        }

        // 5x7 '?' glyph, centered.
        string[] glyph =
        {
            ".###.",
            "#...#",
            "....#",
            "...#.",
            "..#..",
            ".....",
            "..#..",
        };
        Color ink = new(0.88f, 0.88f, 0.88f);
        for (int y = 0; y < glyph.Length; y++)
        {
            for (int x = 0; x < glyph[y].Length; x++)
            {
                if (glyph[y][x] == '#')
                {
                    img.SetPixel(x + 3, y + 2, ink);
                }
            }
        }
        return ImageTexture.CreateFromImage(img);
    }

    private static StyleBoxFlat MakeSlotStyle(bool selected)
    {
        var style = new StyleBoxFlat
        {
            BgColor = selected
                ? new Color(0.30f, 0.30f, 0.36f, 0.85f)
                : new Color(0.08f, 0.08f, 0.10f, 0.55f),
            BorderColor = selected
                ? new Color(1f, 0.92f, 0.55f)
                : new Color(1f, 1f, 1f, 0.25f),
        };
        style.SetBorderWidthAll(selected ? 2 : 1);
        return style;
    }
}
