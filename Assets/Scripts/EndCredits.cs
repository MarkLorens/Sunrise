using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The end credits, after the finale's iris closes to black (FerrisWheel): the Woohoo Land logo fades in and out, then
// each of the team in turn, as a park visitor over their role and name, then a special thanks with a construction
// worker, "And You.", and the studio's logo with a thank you. Then back to the main menu. Each person's look is fixed (body, face, hair and colours picked by their place
// in the list), drawn from the same layered art as the visitors in the park (CharacterSprite). Words are in GameText.
public class EndCredits : MonoBehaviour
{
    const float FadeIn = 1f, Hold = 3f, FadeOut = 0.8f, LogoHold = 3f;
    const float PxPerUnit = 380f; // design px per world unit: a visitor stands about 420 design px tall
    static readonly Color NameInk = new Color32(254, 208, 62, 255); // as in the main menu's Credits

    // How each of the team looks, in GameText.EndCreditsTeam's order: which body (Resources/ArtLineartTorso/
    // body_standard_<n>), face (ArtLineartHead/face_<n>) and hair (ArtLineartHead/hair_<n>), each 1 to 8. Hair does
    // the most to say who's who: 7 is long, 8 is pigtails, 6 is a cap; bodies 2 and 4 have dresses.
    static readonly (int body, int face, int hair)[] Looks =
    {
        (5, 6, 6),
        (2, 7, 7),
        (3, 8, 8),
        (1, 5, 5),
        (8, 4, 4),
        (7, 3, 3),
    };

    RectTransform canvas;

    public static void Play()
    {
        var canvas = MainMenu.Screen("End Credits", 30); // over the HUD and the pause menu
        var black = MainMenu.Ui("Black", canvas);
        MainMenu.Stretch(black);
        MainMenu.Img(black, null, Color.black).raycastTarget = true;
        var credits = canvas.gameObject.AddComponent<EndCredits>();
        credits.canvas = canvas;
        credits.StartCoroutine(credits.Run());
    }

    IEnumerator Run()
    {
        var logo = Page("Logo");
        Figma.Picture(logo.rect, "Logo", "Logo/Wohooland_logo", 756f, 491f, 720f, 590f);
        yield return Show(logo.group, LogoHold);

        var team = GameText.EndCreditsTeam;
        for (int i = 0; i < team.Length; i++)
        {
            var page = Page(team[i].name);
            Visitor(page.rect, i);
            Lines(page.rect, team[i].role, team[i].name);
            yield return Show(page.group, Hold);
        }

        var thanks = Page("Special Thanks");
        Figma.Words(thanks.rect, GameText.EndCreditsThanks, 756f, 150f, Figma.Type.AtmaH1, Color.white, ParkArt.TitleFont);
        var stand = Stand(thanks.rect);
        Layer(stand, Resources.Load<Sprite>("Workers/worker_construction_1_body"), Color.white);
        Layer(stand, Resources.Load<Sprite>("Workers/worker_construction_1_face"), Color.white);
        Lines(thanks.rect, GameText.EndCreditsThanked.role, GameText.EndCreditsThanked.name);
        yield return Show(thanks.group, Hold);

        var you = Page("And You");
        Figma.Words(you.rect, GameText.EndCreditsAndYou, 756f, 491f, Figma.Type.AtmaH1, NameInk, ParkArt.TitleFont);
        yield return Show(you.group, Hold);

        var studio = Page("Studio");
        Figma.Picture(studio.rect, "Studio Logo", "Logo/Max2_Studio", 756f, 430f, 440f, 427f);
        Figma.Words(studio.rect, GameText.EndCreditsThankYou, 756f, 740f, Figma.Type.AtmaH1, Color.white, ParkArt.TitleFont);
        yield return Show(studio.group, LogoHold);

        LoadingScreen.FadeTo(0); // the main menu, first in Build Settings
    }

    // A page of the credits, hidden until Show fades it in.
    (RectTransform rect, CanvasGroup group) Page(string name)
    {
        var rt = MainMenu.Ui(name, canvas);
        MainMenu.Stretch(rt);
        var group = rt.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        return (rt, group);
    }

    // Fades a page in, holds it, fades it out and throws it away. Unscaled, so nothing that slows time slows this.
    // The page itself stays solid and a black cover over it fades instead: faded as a whole, each layer of a visitor
    // went see-through on its own, and the body's outline showed through the face and hair.
    static IEnumerator Show(CanvasGroup page, float hold)
    {
        page.alpha = 1f;
        var cover = MainMenu.Ui("Cover", page.transform);
        MainMenu.Stretch(cover);
        var black = MainMenu.Img(cover, null, Color.black);
        for (float t = 0f; t < FadeIn; t += Time.unscaledDeltaTime) { black.color = new Color(0f, 0f, 0f, 1f - t / FadeIn); yield return null; }
        black.color = Color.clear;
        yield return new WaitForSecondsRealtime(hold);
        for (float t = 0f; t < FadeOut; t += Time.unscaledDeltaTime) { black.color = new Color(0f, 0f, 0f, t / FadeOut); yield return null; }
        Destroy(page.gameObject);
    }

    static void Lines(RectTransform page, string role, string name)
    {
        Figma.Words(page, role, 756f, 760f, Figma.Type.ArvoSubtitle, Color.white, ParkArt.Font);
        Figma.Words(page, name, 756f, 815f, Figma.Type.AtmaH1, NameInk, ParkArt.TitleFont);
    }

    // The point the person stands on (their feet), every layer of them lined up on it by its own pivot.
    static RectTransform Stand(RectTransform page) => Figma.Place(MainMenu.Ui("Stand", page), 756f, 700f, 0f, 0f);

    // A visitor as the park draws one: colour fills under the body's line, then the face, then the hair's fills and
    // line. Person i gets their pick from Looks (body, face and hair i if they have none) and the i-th colour of each
    // clothes palette.
    static void Visitor(RectTransform page, int i)
    {
        var stand = Stand(page);
        (int body, int face, int hair) look = i < Looks.Length ? Looks[i] : (i % 8 + 1, i % 8 + 1, i % 8 + 1);
        string body = $"ArtLineartTorso/body_standard_{look.body}_idle";
        for (int k = 0; k < CharacterSprite.FillLayers.Length; k++)
        {
            var palette = CharacterSprite.Palettes[k];
            // Everyone the same light skin (the palette's first); clothes vary by place in the list.
            var colour = CharacterSprite.FillLayers[k] == "skin" ? palette[0] : palette[i % palette.Length];
            Layer(stand, Resources.Load<Sprite>($"{body}_{CharacterSprite.FillLayers[k]}"), colour);
        }
        Layer(stand, Resources.Load<Sprite>(body), Color.white);
        Layer(stand, Resources.Load<Sprite>($"ArtLineartHead/face_{look.face}_open"), Color.white);
        string hair = $"ArtLineartHead/hair_{look.hair}";
        Layer(stand, Resources.Load<Sprite>(hair + "_hair"), CharacterSprite.HairPalette[i % CharacterSprite.HairPalette.Length]);
        Layer(stand, Resources.Load<Sprite>(hair + "_hat"), CharacterSprite.HatPalette[i % CharacterSprite.HatPalette.Length]);
        Layer(stand, Resources.Load<Sprite>(hair), Color.white);
    }

    // One layer of a person, its pivot on the stand and sized by its pixels per unit (as it is in the park).
    static void Layer(RectTransform stand, Sprite sprite, Color color)
    {
        if (sprite == null) return; // not every hair has a hat
        var rt = MainMenu.Ui(sprite.name, stand);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = sprite.pivot / sprite.rect.size;
        rt.sizeDelta = sprite.rect.size / sprite.pixelsPerUnit * PxPerUnit * Figma.K;
        rt.anchoredPosition = Vector2.zero;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
    }
}
