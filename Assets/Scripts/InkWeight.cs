using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Gives UI text the weight it has in the Figma designs. The project renders in Linear colour space, and there a
// letter's soft edges blend thinner and paler than in Figma (which blends them as sRGB), so dark words on a light
// pill read grey and thin. This draws each letter a few more times, a hair to each side and half see-through, under
// the letter itself: the edges fill back in, in the text's own colour (whatever it's changed to). Every text the UI
// makes gets one (MainMenu.Label, the HUD's and tutorial's text, keycaps).
public class InkWeight : BaseMeshEffect
{
    public float weight = 0.45f;       // how far each copy sits off the letter, in canvas units
    public float copyAlpha = 0.5f;     // how see-through the copies are

    static readonly List<UIVertex> letters = new();
    static readonly List<UIVertex> drawn = new();
    static readonly Vector3[] Offsets = { Vector3.right, Vector3.left, Vector3.up, Vector3.down };

    public static void On(Text text)
    {
        if (text != null && text.GetComponent<InkWeight>() == null) text.gameObject.AddComponent<InkWeight>();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || weight <= 0f) return;
        letters.Clear();
        vh.GetUIVertexStream(letters);
        if (letters.Count == 0) return;
        drawn.Clear();
        foreach (var off in Offsets)
            for (int i = 0; i < letters.Count; i++)
            {
                var v = letters[i];
                v.position += off * weight;
                var c = v.color;
                c.a = (byte)(c.a * copyAlpha);
                v.color = c;
                drawn.Add(v);
            }
        drawn.AddRange(letters); // the letters themselves on top
        vh.Clear();
        vh.AddUIVertexTriangleStream(drawn);
    }
}
