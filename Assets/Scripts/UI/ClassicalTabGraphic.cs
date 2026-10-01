using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A ribbon tab: a banner with a notch cut into each end, a silk grade down its
/// face and a gilt rule along the top and bottom.
/// </summary>
/// <remarks>
/// **Why a ribbon and not a rectangle.** The category strip answers "which part
/// of the library am I in", which is what a bookmark is for -- and the rest of
/// this screen is books, gilding and ribbons. A rectangle is the one shape
/// nothing else here has.
///
/// **Why the two rules are different colours.** The top edge faces the light and
/// the bottom faces away. Gilding of one even brightness reads as a drawn line;
/// the pair reads as a moulded edge, and it is the cheapest way to give a flat
/// shape a thickness.
///
/// Notched at both ends rather than one: the strip scrolls in both directions,
/// and a single notch points somewhere, which would be a claim about which way
/// it is going.
/// </remarks>
[AddComponentMenu("UI/Classical Tab Graphic")]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ClassicalTabGraphic : MaskableGraphic
{
    [Tooltip("兩端 V 形缺口的深度（像素）。")]
    [SerializeField, Range(4f, 40f)] private float notch = 14f;
    [Tooltip("上下兩道金線的粗細。")]
    [SerializeField, Range(0.5f, 6f)] private float rule = 2f;
    [Tooltip("面上的絲光強度。")]
    [SerializeField, Range(0f, 1f)] private float sheen = 0.30f;
    [Tooltip("布面起伏的深度。")]
    [SerializeField, Range(0f, 1f)] private float folds = 0.34f;
    [Tooltip("織紋一格covers 幾個像素。")]
    [SerializeField, Range(16f, 256f)] private float weaveScale = 54f;

    /// <summary>How many strips the ribbon is cut into along its length.</summary>
    private const int Strips = 26;

    /// <summary>外緣柔邊往外鋪多寬（像素）。</summary>
    private const float FeatherWidth = 1f;

    private Texture weave;

    public override Texture mainTexture => weave != null ? weave : base.mainTexture;

    protected override void Awake()
    {
        base.Awake();
        EnsureWeave();
    }

    private void EnsureWeave()
    {
        if (weave != null) return;
        Sprite cloth = ClassicalBookUITheme.GetClothTextureSprite();
        if (cloth == null) return;
        weave = cloth.texture;
        SetMaterialDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        EnsureWeave();
        Rect r = GetPixelAdjustedRect();
        if (r.width <= 8f || r.height <= 6f) return;

        float cut = Mathf.Min(notch, r.width * 0.28f);
        float middle = r.center.y;

        // 絲面：上三分之一亮，往下沉。純色的緞帶沒有面。
        Color top = Tone(1f + sheen);
        Color waist = Tone(1f);
        Color bottom = Tone(1f - sheen * 0.85f);

        // 布不是一塊平板。整條帶子切成細長條，每一條有自己的明暗 —— 幾道緩緩
        // 起伏的皺褶，加上一條比較亮的絲光帶。掛在牆上的緞帶就是這樣：織紋說
        // 它是布，起伏說它是軟的。
        float x0 = r.xMin + cut;
        float x1 = r.xMax - cut;
        for (int i = 0; i < Strips; i++)
        {
            float lo = Mathf.Lerp(x0, x1, i / (float)Strips);
            float hi = Mathf.Lerp(x0, x1, (i + 1) / (float)Strips);
            float fold0 = Fold((lo - x0) / Mathf.Max(1f, x1 - x0));
            float fold1 = Fold((hi - x0) / Mathf.Max(1f, x1 - x0));

            AddQuad(vh, new Vector2(lo, middle), new Vector2(hi, middle),
                new Vector2(hi, r.yMax), new Vector2(lo, r.yMax),
                waist * fold0, waist * fold1, top * fold0, top * fold1);
            AddQuad(vh, new Vector2(lo, r.yMin), new Vector2(hi, r.yMin),
                new Vector2(hi, middle), new Vector2(lo, middle),
                bottom * fold0, bottom * fold1, waist * fold0, waist * fold1);
        }

        // 兩端的尖角。
        AddTriangle(vh, new Vector2(r.xMin + cut, r.yMax), new Vector2(r.xMin + cut, middle),
            new Vector2(r.xMin, middle), top, waist, waist);
        AddTriangle(vh, new Vector2(r.xMin + cut, middle), new Vector2(r.xMin + cut, r.yMin),
            new Vector2(r.xMin, middle), waist, bottom, waist);
        AddTriangle(vh, new Vector2(r.xMax - cut, r.yMax), new Vector2(r.xMax, middle),
            new Vector2(r.xMax - cut, middle), top, waist, waist);
        AddTriangle(vh, new Vector2(r.xMax - cut, middle), new Vector2(r.xMax, middle),
            new Vector2(r.xMax - cut, r.yMin), waist, waist, bottom);

        // 上緣受光、下緣背光的兩道金線。
        Color gilt = ClassicalBookUITheme.Gold;
        Color lit = new Color(gilt.r * 1.35f, gilt.g * 1.3f, gilt.b * 1.15f, color.a);
        Color shaded = new Color(gilt.r * 0.5f, gilt.g * 0.42f, gilt.b * 0.3f, color.a);

        AddQuad(vh,
            new Vector2(r.xMin + cut, r.yMax - rule), new Vector2(r.xMax - cut, r.yMax - rule),
            new Vector2(r.xMax - cut, r.yMax), new Vector2(r.xMin + cut, r.yMax),
            lit, lit, lit, lit);
        AddQuad(vh,
            new Vector2(r.xMin + cut, r.yMin), new Vector2(r.xMax - cut, r.yMin),
            new Vector2(r.xMax - cut, r.yMin + rule), new Vector2(r.xMin + cut, r.yMin + rule),
            shaded, shaded, shaded, shaded);

        Feather(vh, r, x0, x1, middle, top, waist, bottom, lit, shaded);
    }

    /// <summary>
    /// 沿著整圈輪廓往外鋪一圈一像素、淡到全透明的裙邊。
    /// </summary>
    /// <remarks>
    /// 這塊牌子底下那一面每一幀都在縮放，而 MSAA 是關的：硬切的斜邊在縮放途中
    /// 會一幀一幀跳階，看起來像邊緣自己在爬，兩端的尖角最明顯 —— 那裡的邊最
    /// 斜，一個像素的階梯就佔掉整個尖端。多鋪這一像素之後，硬體插值會自己把它
    /// 補成漸層，等於替這個形狀做了一層抗鋸齒。
    ///
    /// 只動邊，不動形：裙邊貼在既有輪廓外面，缺口的幾何一個點都沒有改 —— 缺口
    /// 開在哪一端是有意義的（見類別說明），不是可以順手調的東西。
    ///
    /// 往外推的方向取相鄰兩條邊法線的角平分線，而不是各邊自己的法線：這樣兩塊
    /// 裙邊在轉角共用同一個外側頂點，接得起來，尖端也不會裂出一個楔形的缺口。
    /// </remarks>
    private void Feather(VertexHelper vh, Rect r, float x0, float x1, float middle,
        Color top, Color waist, Color bottom, Color lit, Color shaded)
    {
        Vector2[] ring =
        {
            new Vector2(x0, r.yMax), new Vector2(x1, r.yMax), new Vector2(r.xMax, middle),
            new Vector2(x1, r.yMin), new Vector2(x0, r.yMin), new Vector2(r.xMin, middle),
        };
        // 每一條邊在輪廓上的真實顏色。柔邊要從它自己淡出去，否則等於沿著邊鑲了
        // 一條別的顏色的線，比鋸齒還顯眼。
        Color[] from = { lit, top, waist, shaded, bottom, waist };
        Color[] to = { lit, waist, bottom, shaded, waist, top };

        var outward = new Vector2[ring.Length];
        for (int i = 0; i < ring.Length; i++)
        {
            Vector2 next = ring[(i + 1) % ring.Length];
            Vector2 edge = next - ring[i];
            Vector2 n = new Vector2(edge.y, -edge.x).normalized;
            // 法線有兩個方向，取背對中心的那一個。這樣寫就不必假設頂點是順時針
            // 還是逆時針排的。
            if (Vector2.Dot(n, (ring[i] + next) * 0.5f - r.center) < 0f) n = -n;
            outward[i] = n;
        }

        var push = new Vector2[ring.Length];
        for (int i = 0; i < ring.Length; i++)
        {
            Vector2 bisector = (outward[(i + ring.Length - 1) % ring.Length] + outward[i]).normalized;
            // 夾角越尖，要推得越遠才補得滿；但尖端的 1/cos 會衝到很大，夾住它，
            // 免得柔邊在那裡長出一根刺。
            float lean = Mathf.Max(0.35f, Vector2.Dot(bisector, outward[i]));
            push[i] = bisector * (FeatherWidth / lean);
        }

        for (int i = 0; i < ring.Length; i++)
        {
            int j = (i + 1) % ring.Length;
            AddQuad(vh, ring[i], ring[j], ring[j] + push[j], ring[i] + push[i],
                from[i], to[i], Fade(to[i]), Fade(from[i]));
        }
    }

    /// <summary>同一個顏色，但完全透明 —— 柔邊外側的端點。</summary>
    private static Color Fade(Color c)
    {
        return new Color(c.r, c.g, c.b, 0f);
    }

    /// <summary>
    /// How much light this point along the ribbon is catching.
    /// </summary>
    /// <remarks>
    /// Two waves of unrelated wavelength: one slow, which is the ribbon lying in
    /// a shallow S, and one quicker, which is the small creasing any woven thing
    /// has. Both are gentle -- cloth this size does not fold, it undulates, and
    /// anything stronger reads as a corrugated sheet.
    /// </remarks>
    private float Fold(float t)
    {
        float slow = Mathf.Sin(t * Mathf.PI * 2.2f + 0.6f);
        float quick = Mathf.Sin(t * Mathf.PI * 7.3f + 2.1f);
        return 1f + (slow * 0.62f + quick * 0.38f) * folds * 0.5f;
    }

    /// <summary>The tab's own colour at a given brightness, alpha preserved.</summary>
    private Color Tone(float scale)
    {
        return new Color(Mathf.Clamp01(color.r * scale), Mathf.Clamp01(color.g * scale),
            Mathf.Clamp01(color.b * scale), color.a);
    }

    private void AddQuad(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
        Color c0, Color c1, Color c2, Color c3)
    {
        int index = vh.currentVertCount;
        AddVertex(vh, p0, c0);
        AddVertex(vh, p1, c1);
        AddVertex(vh, p2, c2);
        AddVertex(vh, p3, c3);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index + 2, index + 3, index);
    }

    private void AddTriangle(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2,
        Color c0, Color c1, Color c2)
    {
        int index = vh.currentVertCount;
        AddVertex(vh, p0, c0);
        AddVertex(vh, p1, c1);
        AddVertex(vh, p2, c2);
        vh.AddTriangle(index, index + 1, index + 2);
    }

    /// <summary>
    /// Adds a vertex, with the weave coordinate taken from where it sits.
    /// </summary>
    /// <remarks>
    /// From the position rather than from the quad's corners: the threads then
    /// run at one size along the whole ribbon instead of stretching to fit each
    /// strip, which is the give-away that would undo the whole effect.
    /// </remarks>
    private void AddVertex(VertexHelper vh, Vector2 position, Color colour)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = colour;
        vertex.uv0 = new Vector2(position.x / weaveScale, position.y / weaveScale);
        vh.AddVert(vertex);
    }
}
