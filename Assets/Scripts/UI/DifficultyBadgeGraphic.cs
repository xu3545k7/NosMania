using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 遊玩畫面上的難度牌：整塊都是該難度的顏色，上亮下暗的漸層，四角收圓。
/// </summary>
/// <remarks>
/// **為什麼整塊上色，而不是只把字染成難度色。** 這一塊要在一眼掃過去的時候就答完
/// 「這是什麼難度」，而字的面積太小 —— 一行有顏色的字和一塊有顏色的牌，後者在餘光
/// 裡還讀得到。字本身改成白的，讓顏色只由底板負責：兩個都帶色的話，深色難度（紅、
/// 紫）上的同色字會糊掉。
///
/// **為什麼是漸層不是平色。** 平色的方塊是一個 UI 元件；有上下明暗的方塊是一塊做出
/// 來的牌子。這個畫面上其他東西（紙卡、簾幕、木條）都有明暗，只有它平的話它會讀成
/// 貼上去的。
///
/// **為什麼圓角用逐條橫帶而不是九宮格。** 牌的尺寸會隨等級位數變（一位數和兩位數不
/// 一樣寬），九宮格要一張貼圖而且圓角半徑固定；逐條算 inset 的話半徑是個常數，任何
/// 尺寸都對，而且漸層順便在同一個迴圈裡做完。
/// </remarks>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class DifficultyBadgeGraphic : MaskableGraphic
{
    /// <summary>四角的圓半徑。</summary>
    private const float Radius = 10f;

    /// <summary>橫帶數。圓角是靠這個解析出來的，太少會變成階梯。</summary>
    private const int Strips = 28;

    [SerializeField] private Color ink = new Color(0.78f, 0.22f, 0.24f, 1f);

    /// <summary>這一階難度的顏色。整塊牌由它決定。</summary>
    public Color Ink
    {
        get => ink;
        set
        {
            if (ink == value) return;
            ink = value;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width < 8f || r.height < 8f) return;

        float radius = Mathf.Min(Radius, Mathf.Min(r.width, r.height) * 0.5f);
        // 上亮下暗。往白的方向提亮而不是加飽和度：紅和紫再飽和就只會更黑。
        Color top = Color.Lerp(ink, Color.white, 0.30f);
        Color bottom = new Color(ink.r * 0.52f, ink.g * 0.52f, ink.b * 0.52f, ink.a);

        for (int i = 0; i < Strips; i++)
        {
            float t0 = i / (float)Strips;
            float t1 = (i + 1f) / Strips;
            float y0 = Mathf.Lerp(r.yMax, r.yMin, t0);
            float y1 = Mathf.Lerp(r.yMax, r.yMin, t1);
            float in0 = Inset(y0, r, radius);
            float in1 = Inset(y1, r, radius);
            Color c0 = Color.Lerp(top, bottom, t0);
            Color c1 = Color.Lerp(top, bottom, t1);

            Quad(vh,
                new Vector2(r.xMin + in0, y0), new Vector2(r.xMax - in0, y0),
                new Vector2(r.xMax - in1, y1), new Vector2(r.xMin + in1, y1),
                c0, c0, c1, c1);
        }

        // 上緣一道受光：牌子的上面是朝著燈的那一面。
        Color sheen = Color.Lerp(top, Color.white, 0.34f);
        Color gone = sheen; gone.a = 0f;
        float lip = Mathf.Min(r.height * 0.18f, 7f);
        float inTop = Inset(r.yMax, r, radius);
        float inLip = Inset(r.yMax - lip, r, radius);
        Quad(vh,
            new Vector2(r.xMin + inTop, r.yMax), new Vector2(r.xMax - inTop, r.yMax),
            new Vector2(r.xMax - inLip, r.yMax - lip), new Vector2(r.xMin + inLip, r.yMax - lip),
            sheen, sheen, gone, gone);
    }

    /// <summary>這一條橫帶的左右各要縮進多少，四角才是圓的。</summary>
    private static float Inset(float y, Rect r, float radius)
    {
        if (radius <= 0f) return 0f;
        float d = Mathf.Min(r.yMax - y, y - r.yMin);
        if (d >= radius) return 0f;
        return radius - Mathf.Sqrt(Mathf.Max(0f, radius * radius - (radius - d) * (radius - d)));
    }

    private static void Quad(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
        Color c0, Color c1, Color c2, Color c3)
    {
        int at = vh.currentVertCount;
        Add(vh, p0, c0);
        Add(vh, p1, c1);
        Add(vh, p2, c2);
        Add(vh, p3, c3);
        vh.AddTriangle(at, at + 1, at + 2);
        vh.AddTriangle(at + 2, at + 3, at);
    }

    private static void Add(VertexHelper vh, Vector2 position, Color colour)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = colour;
        vh.AddVert(vertex);
    }
}
