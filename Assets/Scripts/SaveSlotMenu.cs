using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The pause menu's Save Game: "SELECT SLOT" on the blue panel, a row per save (the objective you were on, when you
// last played, as on the Load Game cards) and a [NEW SLOT] row last. The row picked sits on the yellow brush stroke
// (UI Asset/Buttons/Button3_Active) in
// dark writing, the rest in pale lilac. Visible rows show at once; the mouse wheel, or up/down past the end, scrolls.
// Picking the slot being played (or [NEW SLOT]) saves straight away; another saved slot asks first (ConfirmDialog).
// Cancel, or Esc, closes it. `done` hears true (saved), false (couldn't save) or null (cancelled).
public class SaveSlotMenu : MonoBehaviour
{
    public static bool IsOpen => open != null;
    static SaveSlotMenu open;

    class Row
    {
        public int slot;
        public bool saved;
        public Button button;
        public RectTransform rt;
        public Image stroke;
        public Text title, played;
    }

    // Measured off the design: the title 192 above the panel's middle; the list's top 158 above it, the picked row 164
    // tall (its stroke 121, a touch above its middle), the others 64; names at -276 in Atma 35, dates at +127 in
    // Arvo 21; Cancel 197 below.
    const int Visible = 3;
    const float PanelW = 880f, PanelH = 600f, TitleY = 192f, ListTop = 158f, RowH = 64f, PickedH = 164f;
    const float NameX = -276f, DateX = 127f, StrokeW = 824f, StrokeH = 121f, CancelY = -197f;
    static readonly Color Pale = new(180f / 255f, 182f / 255f, 230f / 255f);

    readonly List<Row> rows = new();
    RectTransform list;
    Button cancel;
    Texture2D shot;
    int current, first, order;
    int confirmClosedFrame = -1;
    Action<bool?> done;

    public static void Open(int order, Texture2D shot, Action<bool?> done)
    {
        if (open != null) return;
        var canvas = MainMenu.Screen("Save Slot", order);
        open = canvas.gameObject.AddComponent<SaveSlotMenu>();
        open.order = order;
        open.shot = shot;
        open.done = done;
        open.Build(canvas);
    }

    void Build(RectTransform canvas)
    {
        var block = MainMenu.Ui("Block", canvas); // clicks outside the panel go nowhere
        MainMenu.Stretch(block);
        MainMenu.Img(block, null, Color.clear).raycastTarget = true;

        var panel = Place(MainMenu.Ui("Panel", canvas), Vector2.zero, new Vector2(PanelW, PanelH));
        MainMenu.Img(panel, "UI/Menu/bg_main", Color.white).pixelsPerUnitMultiplier = 0.8f;
        MainMenu.Label(Place(MainMenu.Ui("Title", panel), new Vector2(0f, TitleY), new Vector2(600f, 60f)), "SELECT SLOT", Figma.Type.AtmaH2, Color.white, ParkArt.TitleFont);
        list = Place(MainMenu.Ui("Rows", panel), Vector2.zero, new Vector2(PanelW, PanelH));

        foreach (var slot in SaveGame.Saved()) rows.Add(MakeRow(slot, true));
        rows.Add(MakeRow(SaveGame.FirstEmpty, false));
        cancel = MainMenu.Pill(panel, "Cancel", Cancel, 205f, 56f, e => e.backButton); // S-06: it backs out
        Place((RectTransform)cancel.transform, new Vector2(0f, CancelY), new Vector2(205f, 56f));

        // Start on the slot being played, or [NEW SLOT] if it hasn't been saved yet.
        current = rows.FindIndex(r => r.saved && r.slot == SaveGame.Slot);
        if (current < 0) current = rows.Count - 1;
        first = Mathf.Clamp(current - Visible + 1, 0, Mathf.Max(0, rows.Count - Visible));
        Refresh();
        MainMenu.Select(rows[current].button);
    }

    static RectTransform Place(RectTransform rt, Vector2 at, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = at;
        rt.sizeDelta = size;
        return rt;
    }

    Row MakeRow(int slot, bool saved)
    {
        var r = new Row { slot = slot, saved = saved, rt = MainMenu.Ui("Slot " + (slot + 1), list) };
        r.rt.anchorMin = r.rt.anchorMax = r.rt.pivot = new Vector2(0.5f, 1f);
        var hit = r.rt.gameObject.AddComponent<Image>(); // the whole row is clickable, see-through
        hit.color = Color.clear;
        r.button = r.rt.gameObject.AddComponent<Button>();
        r.button.targetGraphic = hit;
        r.button.transition = Selectable.Transition.None;
        r.button.onClick.AddListener(() => Pick(r)); // its sound: Pick
        r.rt.gameObject.AddComponent<MainMenu.HoverSelects>();
        r.rt.gameObject.AddComponent<RowKeys>().menu = this;

        var stroke = Place(MainMenu.Ui("Stroke", r.rt), new Vector2(0f, 3f), new Vector2(StrokeW, StrokeH));
        r.stroke = MainMenu.Img(stroke, null, Color.white);
        r.stroke.sprite = UiArt.Get("Buttons/Button3_Active"); // the artist's yellow stroke, stretched along the row

        var info = saved ? SaveGame.Read(slot) : null;
        if (info?.picture != null) Destroy(info.picture); // no pictures here
        r.title = MainMenu.Label(Line(r.rt, "Objective", NameX, 420f), info == null ? "[NEW SLOT]" : string.IsNullOrEmpty(info.title) ? "Saved game" : info.title, Figma.Type.AtmaBody, Pale, ParkArt.TitleFont);
        r.played = MainMenu.Label(Line(r.rt, "Played", DateX, 260f), info == null ? "" : info.played.ToString("d MMM yyyy, HH:mm"), Figma.Type.ArvoBody, Pale, ParkArt.Font);
        r.title.alignment = r.played.alignment = TextAnchor.MiddleLeft;
        return r;
    }

    // A left-aligned line of text starting `x` from the row's middle.
    static RectTransform Line(RectTransform row, string name, float x, float width)
    {
        var rt = MainMenu.Ui(name, row);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(width, 50f);
        return rt;
    }

    // Only the rows in the window show, stacked down from ListTop; the picked one is taller, on the stroke, in dark
    // writing.
    void Refresh()
    {
        float y = ListTop;
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            bool showing = i >= first && i < first + Visible, on = i == current;
            r.rt.gameObject.SetActive(showing);
            float h = on ? PickedH : RowH;
            r.rt.sizeDelta = new Vector2(PanelW - 60f, h);
            if (showing)
            {
                r.rt.anchoredPosition = new Vector2(0f, y - PanelH * 0.5f);
                y -= h;
            }
            r.stroke.enabled = on;
            r.title.color = r.played.color = on ? MainMenu.Ink : Pale;
            r.button.navigation = new Navigation { mode = Navigation.Mode.None }; // up/down: RowKeys
        }
        cancel.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = rows[current].button };
    }

    // Up/down from a row: the next row, scrolling if it isn't showing; down from the last goes to Cancel.
    void Step(int dir)
    {
        int to = current + dir;
        if (to >= rows.Count)
        {
            MainMenu.Select(cancel);
            return;
        }
        if (to < 0) return;
        current = to;
        if (current < first) first = current;
        if (current >= first + Visible) first = current - Visible + 1;
        Refresh();
        MainMenu.Select(rows[current].button);
    }

    class RowKeys : MonoBehaviour, IMoveHandler
    {
        public SaveSlotMenu menu;

        public void OnMove(AxisEventData e)
        {
            if (e.moveDir == MoveDirection.Up) menu.Step(-1);
            else if (e.moveDir == MoveDirection.Down) menu.Step(1);
        }
    }

    // Saving straight away plays the Save Button sound once it's saved (SaveInto); asking first plays the click (G-03),
    // and Yes is quiet until the save's done.
    void Pick(Row r)
    {
        if (!r.saved || r.slot == SaveGame.Slot)
        {
            SaveInto(r.saved ? r.slot : SaveGame.FirstEmpty);
            return;
        }
        AudioManager.TryPlay(e => e.menuClick);
        ConfirmDialog.Open(order + 1, "OVERWRITE SLOT", "Are you sure?", yes =>
        {
            confirmClosedFrame = Time.frameCount;
            if (yes) SaveInto(r.slot);
            else MainMenu.Select(r.button);
        }, yesSound: e => default);
    }

    // From now on that's the slot being played (and auto saved into). Saved: the Save Button sound (S-07).
    void SaveInto(int slot)
    {
        SaveGame.Slot = slot;
        bool saved = SaveGame.Save(shot);
        if (saved) AudioManager.TryPlay(e => e.saveButton);
        Finish(saved);
    }

    void Cancel() => Finish(null);

    void Finish(bool? result)
    {
        if (open != this) return;
        open = null;
        Destroy(gameObject);
        done?.Invoke(result);
    }

    void Update()
    {
        if (ConfirmDialog.IsOpen || Time.frameCount == confirmClosedFrame) return;

        var picked = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        for (int i = 0; i < rows.Count; i++)
            if (picked == rows[i].button.gameObject && current != i)
            {
                current = i;
                Refresh();
            }
        if (picked == null) MainMenu.Select(rows[current].button); // clicked on nothing

        // The mouse wheel scrolls the list a row at a time.
        float wheel = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
        if (Mathf.Abs(wheel) > 0.01f && rows.Count > Visible)
        {
            first = Mathf.Clamp(first - (int)Mathf.Sign(wheel), 0, rows.Count - Visible);
            current = Mathf.Clamp(current, first, first + Visible - 1);
            Refresh();
            MainMenu.Select(rows[current].button);
        }

        if (Controls.Back)
        {
            AudioManager.TryPlay(e => e.backButton); // S-06, as if Cancel was clicked
            Cancel();
        }
    }

    void OnDestroy()
    {
        if (open == this) open = null;
    }
}
