using Raylib_cs;

/// <summary>
/// A vertical list of options in the bottom-left corner with a ">" cursor. Up/Down (or W/S) move the cursor,
/// Enter/Space confirm, and each option's hotkey selects and confirms it in one press. Disabled options are
/// drawn greyed out and skipped by the cursor.
/// </summary>
class CursorMenu
{
    public record Item(KeyboardKey Hotkey, string Label, Action OnSelect, Func<bool>? Enabled = null)
    {
        public bool IsEnabled => Enabled?.Invoke() ?? true;
    }

    const int FontSize = 28;
    const int LineGap = 14;
    const int MarginLeft = 40;
    const int MarginBottom = 50;
    const int CursorWidth = 34; // room for the "> " cursor so labels stay aligned whether or not they're selected

    readonly Item[] items;
    int cursor;

    public CursorMenu(params Item[] items) => this.items = items;

    /// <summary>Puts the cursor on the first enabled item; call when the menu is (re)opened.</summary>
    public void Reset()
    {
        cursor = 0;
        if (!items[cursor].IsEnabled) Move(+1);
    }

    /// <summary>Puts the cursor on the given item if it's enabled, else on the first enabled one.</summary>
    public void Reset(KeyboardKey preferred)
    {
        int i = Array.FindIndex(items, it => it.Hotkey == preferred);
        if (i >= 0 && items[i].IsEnabled) cursor = i;
        else Reset();
    }

    public void Update()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S)) Move(+1);
        if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W)) Move(-1);

        for (int i = 0; i < items.Length; i++)
        {
            if (Raylib.IsKeyPressed(items[i].Hotkey) && items[i].IsEnabled)
            {
                cursor = i;
                items[i].OnSelect();
                return;
            }
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space))
            items[cursor].OnSelect();
    }

    void Move(int step)
    {
        do cursor = (cursor + step + items.Length) % items.Length;
        while (!items[cursor].IsEnabled);
    }

    public void Draw()
    {
        int y = Screen.Height - MarginBottom - items.Length * FontSize - (items.Length - 1) * LineGap;
        for (int i = 0; i < items.Length; i++)
        {
            bool selected = i == cursor;
            Color color = !items[i].IsEnabled ? Color.DarkGray : selected ? Color.White : Color.Gray;
            if (selected) Raylib.DrawText(">", MarginLeft, y, FontSize, Color.White);
            Raylib.DrawText(items[i].Label, MarginLeft + CursorWidth, y, FontSize, color);
            y += FontSize + LineGap;
        }
    }
}
