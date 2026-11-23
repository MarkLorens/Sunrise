using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Load Game, from the main menu: a card for every save (SaveGame.Saved), on the dark blue, and [NEW GAME] cards to
// make up Visible when there are fewer saves than that. Visible cards show at once, a carousel: the arrows either side
// (UI/Buttons/key_back, flipped for the right one), or the left/right keys past the end, slide along one at a time.
// A saved card shows the objective you were on, when you last played and a round picture of the park. The card picked
// is yellow (UI/Menu/bg_selected), the others blue (bg_main). Picking a card loads that slot, or starts a new game in
// it. Delete Slot sits under the picked card when it has a save, and asks first (ConfirmDialog).
// Back, or Esc, closes it.
public class LoadGameMenu : MonoBehaviour
{
    static LoadGameMenu open;
    public static bool IsOpen => open != null;

    class Card
    {
        public int slot;
        public Button button;
        public RectTransform rt;
        public Image art;
        public RawImage picture;
        public Text title, played;
        public bool saved;
    }

    // Measured off the design: the cards side by side in the middle of the screen, the picked one 6% bigger; on each,
    // the picture's middle 90 above the card's, the objective's two lines round 120 below, the date 192 below.
    const int Visible = 3;
    const float CardW = 460f, CardH = 634f, CardY = 0f, CardGap = 458f, PickedScale = 1.06f;
    const float TitleY = -120f, EmptyTitleY = -100f, PlayedY = -192f;
    // The cards' insides (inside the drawn edge) sit 8.5 px of their 370 px art left of the middle: the picture with them.
    const float PictureX = -11f;
    const float ArrowX = CardGap + CardW * 0.5f + 80f, ArrowW = 100f, ArrowH = 80f;
    static readonly Color Empty = new(0.85f, 0.85f, 0.85f);
    static Texture2D disc;

    readonly List<Card> cards = new();
    readonly List<Texture2D> pictures = new();
    RectTransform canvas;
    Button deleteButton, backButton, leftArrow, rightArrow;
    int current, first; // the card picked; the first card showing
    int order;
    Action onClose;
    int dialogClosedFrame = -1;

    public static void Open(int order, Action onClose)
    {
        if (open != null) return;
        var canvas = MainMenu.Screen("Load Game", order);
        open = canvas.gameObject.AddComponent<LoadGameMenu>();
        open.order = order;
        open.onClose = onClose;
        open.Build(canvas);
    }

    void Build(RectTransform canvas)
    {
        this.canvas = canvas;
        var bg = MainMenu.Ui("Background", canvas);
        MainMenu.Stretch(bg);
        MainMenu.Img(bg, null, MainMenu.Night).raycastTarget = true; // nothing behind it gets clicked

        deleteButton = MainMenu.Pill(canvas, "Delete Slot", AskDelete, 285f, 56f);
        backButton = MainMenu.Pill(canvas, "Back", Close, 205f, 56f, e => e.backButton); // S-06, like the other Backs
        Place(backButton.transform, new Vector2(650f, -435f));
        leftArrow = Arrow(-1);
        rightArrow = Arrow(1);
        MakeCards();
        if (cards.Count > 0) MainMenu.Select(cards[current].button);
    }

    static void Place(Transform t, Vector2 at)
    {
        var rt = (RectTransform)t;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = at;
    }

    // The hand-drawn "<" (the right one mirrored): slides the cards one along.
    Button Arrow(int dir)
    {
        var rt = MainMenu.Ui(dir < 0 ? "Left" : "Right", canvas);
        Place(rt, new Vector2(dir * ArrowX, CardY));
        rt.sizeDelta = new Vector2(ArrowW, ArrowH);
        rt.localScale = new Vector3(-dir, 1f, 1f);
        var img = MainMenu.Img(rt, "UI/Buttons/key_back", Color.white);
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.raycastTarget = true;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.8f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None }; // the keys slide with left/right on a card
        button.onClick.AddListener(() => AudioManager.TryPlay(e => e.toggle)); // S-04
        button.onClick.AddListener(() => Slide(dir));
        return button;
    }

    // ---------------------------------------------------------------- the cards

    void MakeCards()
    {
        foreach (var c in cards) Destroy(c.rt.gameObject);
        cards.Clear();
        var saved = SaveGame.Saved();
        foreach (var slot in saved) cards.Add(MakeCard(slot));
        for (int free = 0; cards.Count < Visible; free++) // fewer saves than Visible: [NEW GAME] in empty slots
            if (!saved.Contains(free)) cards.Add(MakeCard(free));
        current = Mathf.Clamp(current, 0, cards.Count - 1);
        first = Mathf.Clamp(first, 0, Mathf.Max(0, cards.Count - Visible));
        if (current < first) first = current;
        if (current >= first + Visible) first = current - Visible + 1;
        Refresh();
    }

    Card MakeCard(int slot)
    {
        var c = new Card { slot = slot, rt = MainMenu.Ui("Slot " + (slot + 1), canvas) };
        c.rt.SetSiblingIndex(1); // over the background, under the buttons
        Place(c.rt, Vector2.zero);
        c.rt.sizeDelta = new Vector2(CardW, CardH);
        c.art = c.rt.gameObject.AddComponent<Image>();
        c.art.preserveAspect = true; // the whole drawing, edges and all
        c.button = c.rt.gameObject.AddComponent<Button>();
        c.button.transition = Selectable.Transition.None;
        c.button.onClick.AddListener(() => Pick(c));
        c.rt.gameObject.AddComponent<MainMenu.HoverSelects>();
        c.rt.gameObject.AddComponent<CardKeys>().menu = this;

        var pic = MainMenu.Ui("Picture", c.rt);
        Place(pic, new Vector2(PictureX, 90f));
        pic.sizeDelta = new Vector2(260f, 260f);
        c.picture = pic.gameObject.AddComponent<RawImage>();
        c.picture.raycastTarget = false;

        var title = MainMenu.Ui("Objective", c.rt);
        Place(title, new Vector2(0f, TitleY));
        title.sizeDelta = new Vector2(300f, 100f);
        c.title = MainMenu.Label(title, "", Figma.Type.AtmaBody, Color.white, ParkArt.TitleFont);
        c.title.horizontalOverflow = HorizontalWrapMode.Wrap;
        c.title.lineSpacing = 0.85f;

        var played = MainMenu.Ui("Played", c.rt);
        Place(played, new Vector2(0f, PlayedY));
        played.sizeDelta = new Vector2(360f, 40f);
        c.played = MainMenu.Label(played, "", Figma.Type.ArvoBody, Color.white, ParkArt.Font);

        var info = SaveGame.Read(slot);
        c.saved = info != null;
        if (info?.picture != null) pictures.Add(info.picture);
        c.picture.texture = info?.picture != null ? info.picture : Disc();
        c.picture.color = info?.picture != null ? Color.white : Empty;
        c.title.text = c.saved ? (string.IsNullOrEmpty(info.title) ? "Saved game" : info.title).ToUpperInvariant() : "[NEW GAME]";
        c.title.rectTransform.anchoredPosition = new Vector2(0f, c.saved ? TitleY : EmptyTitleY);
        c.played.text = c.saved ? info.played.ToString("d MMM yyyy, HH:mm") : "";
        return c;
    }

    // A plain white circle, tinted grey for an empty slot's picture.
    static Texture2D Disc()
    {
        if (disc != null) return disc;
        const int size = 128;
        disc = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - r, y + 0.5f - r).magnitude;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255f));
            }
        disc.SetPixels32(px);
        disc.Apply();
        return disc;
    }

    void Pick(Card c)
    {
        AudioManager.TryPlay(e => e.newOrLoadGame);
        if (c.saved)
        {
            MainMenu.CutLobbyMusic();
            SaveGame.Load(c.slot);
        }
        else MainMenu.StartIn(c.slot); // cuts it too
    }

    // The arrows: the cards slide one along; the picked card stays picked if it's still showing.
    void Slide(int dir)
    {
        first = Mathf.Clamp(first + dir, 0, Mathf.Max(0, cards.Count - Visible));
        current = Mathf.Clamp(current, first, first + Visible - 1);
        Refresh();
        MainMenu.Select(cards[current].button);
    }

    // Left/right on a card: the next card along, sliding them if it isn't showing.
    void Step(int dir)
    {
        int to = Mathf.Clamp(current + dir, 0, cards.Count - 1);
        if (to == current) return;
        current = to;
        if (current < first) first = current;
        if (current >= first + Visible) first = current - Visible + 1;
        Refresh();
        MainMenu.Select(cards[current].button);
    }

    class CardKeys : MonoBehaviour, IMoveHandler
    {
        public LoadGameMenu menu;

        public void OnMove(AxisEventData e)
        {
            if (e.moveDir == MoveDirection.Left) menu.Step(-1);
            else if (e.moveDir == MoveDirection.Right) menu.Step(1);
        }
    }

    void Update()
    {
        if (cards.Count == 0) return; // closing
        var picked = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        for (int i = 0; i < cards.Count; i++)
            if (picked == cards[i].button.gameObject && current != i)
            {
                current = i;
                Refresh();
            }
        if (picked == null && !ConfirmDialog.IsOpen) MainMenu.Select(cards[current].button); // clicked on nothing

        for (int i = 0; i < cards.Count; i++)
        {
            float to = i == current ? PickedScale : 1f;
            float s = Mathf.MoveTowards(cards[i].rt.localScale.x, to, Time.unscaledDeltaTime * 0.6f);
            cards[i].rt.localScale = new Vector3(s, s, 1f);
        }

        if (!ConfirmDialog.IsOpen && Time.frameCount != dialogClosedFrame && Controls.Back)
        {
            AudioManager.TryPlay(e => e.backButton); // S-06, as if Back was clicked
            Close();
        }
    }

    // Only the cards in the window show, side by side; the picked one goes yellow with dark writing, Delete Slot moves
    // under it (if it has a save), the arrows show when there's more that way, and the keys go where they look like
    // they should.
    void Refresh()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            bool showing = i >= first && i < first + Visible;
            c.rt.gameObject.SetActive(showing);
            int shown = Mathf.Min(Visible, cards.Count); // fewer saves than that: still in the middle
            c.rt.anchoredPosition = new Vector2((i - first - (shown - 1) * 0.5f) * CardGap, CardY);
            bool on = i == current;
            c.art.sprite = ParkArt.Get(on ? "UI/Menu/bg_selected" : "UI/Menu/bg_main");
            c.art.type = Image.Type.Simple;
            c.title.color = c.played.color = on ? MainMenu.Ink : Color.white;
            c.button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = c.saved ? deleteButton : backButton };
        }
        leftArrow.gameObject.SetActive(first > 0);
        rightArrow.gameObject.SetActive(first + Visible < cards.Count);

        var card = cards[current];
        deleteButton.gameObject.SetActive(card.saved);
        Place(deleteButton.transform, new Vector2(card.rt.anchoredPosition.x, CardY - CardH * 0.5f - 70f));
        deleteButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = card.button, selectOnRight = backButton };
        backButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = card.button, selectOnLeft = card.saved ? deleteButton : card.button };
    }

    // ---------------------------------------------------------------- deleting a slot

    void AskDelete()
    {
        if (!cards[current].saved || ConfirmDialog.IsOpen) return;
        ConfirmDialog.Open(order + 1, "DELETE SLOT", "Are you sure?", yes =>
        {
            dialogClosedFrame = Time.frameCount;
            if (yes)
            {
                SaveGame.Delete(cards[current].slot);
                MakeCards(); // one card fewer (or [NEW GAME] in its place)
            }
            else Refresh();
            MainMenu.Select(cards[current].button);
        }, yesSound: e => e.deleteSaveFile);
    }

    void Close()
    {
        Destroy(gameObject);
        onClose?.Invoke();
    }

    void OnDestroy()
    {
        if (open == this) open = null;
        foreach (var p in pictures) if (p != null) Destroy(p);
    }
}
