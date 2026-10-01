using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 考程那兩條階梯的記號：技階是一塊刻著羅馬數字的黃銅牌，檢定是一枚朱紅印章。
/// </summary>
/// <remarks>
/// **為什麼不用字。** 頂欄那一條已經有「切換至演奏會」「練習室」「實力評分」
/// 「MAXIMUM Lv.15」「設定」五組字，再塞兩組就只是第六第七串字 —— 掃過去分不出哪
/// 個是哪個。兩枚記號的<b>剪影</b>不同（方牌 vs 圓印）、<b>顏色</b>不同（黃銅 vs
/// 朱砂），所以在 56px 上不必讀字就認得出來。
///
/// **為什麼數字本身就是圖。** 羅馬數字是由 I、V、X 三個筆畫組成的，所以「第幾階」不
/// 需要另外畫一個進度指示 —— 刻在牌上的那幾道就是。印章上的級數同理：一枚圓印，中
/// 間一個數字，外圈的齒和內圈決定等第（及第／優等／特優），三件事一眼看完。
///
/// **空狀態是空的牌和空的印，不是破折號。** 還沒敘階就是一塊沒刻字的牌，還沒受檢就
/// 是一枚沒蓋過的空印 —— 那本來就是這兩樣東西在「還沒發生」時的樣子。
/// </remarks>
internal static class SyllabusIcons
{
    internal static readonly Color Brass = new Color(0.86f, 0.71f, 0.40f, 1f);
    internal static readonly Color BrassDeep = new Color(0.46f, 0.34f, 0.16f, 1f);
    internal static readonly Color Engrave = new Color(0.16f, 0.10f, 0.045f, 1f);
    internal static readonly Color Vermilion = new Color(0.72f, 0.20f, 0.13f, 1f);
    internal static readonly Color VermilionDim = new Color(0.44f, 0.26f, 0.22f, 0.75f);

    /// <summary>一段有厚度的直線。所有筆畫都是這個。</summary>
    internal static void Bar(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color colour)
    {
        Vector2 along = b - a;
        if (along.sqrMagnitude < 1e-6f) return;
        Vector2 side = new Vector2(-along.y, along.x).normalized * thickness * 0.5f;
        Quad(vh, a - side, a + side, b + side, b - side, colour);
    }

    internal static void Quad(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
        Color colour)
    {
        int at = vh.currentVertCount;
        Add(vh, p0, colour);
        Add(vh, p1, colour);
        Add(vh, p2, colour);
        Add(vh, p3, colour);
        vh.AddTriangle(at, at + 1, at + 2);
        vh.AddTriangle(at + 2, at + 3, at);
    }

    /// <summary>圓盤（或圓環的一段）。<paramref name="inner"/> 0 就是實心。</summary>
    internal static void Ring(VertexHelper vh, Vector2 centre, float inner, float outer,
        Color colour, int steps = 48)
    {
        for (int i = 0; i < steps; i++)
        {
            float a0 = Mathf.PI * 2f * i / steps;
            float a1 = Mathf.PI * 2f * (i + 1) / steps;
            Vector2 d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
            Vector2 d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
            Quad(vh, centre + d0 * inner, centre + d0 * outer,
                centre + d1 * outer, centre + d1 * inner, colour);
        }
    }

    /// <summary>四角切斜的板子。鑄件的角不是直角，這一刀讓它不像一個 UI 方塊。</summary>
    internal static void Plate(VertexHelper vh, Rect r, float chamfer, Color colour)
    {
        float x0 = r.xMin, x1 = r.xMax, y0 = r.yMin, y1 = r.yMax;
        float c = Mathf.Min(chamfer, Mathf.Min(r.width, r.height) * 0.4f);
        var points = new[]
        {
            new Vector2(x0 + c, y0), new Vector2(x1 - c, y0), new Vector2(x1, y0 + c),
            new Vector2(x1, y1 - c), new Vector2(x1 - c, y1), new Vector2(x0 + c, y1),
            new Vector2(x0, y1 - c), new Vector2(x0, y0 + c),
        };
        Vector2 centre = r.center;
        for (int i = 0; i < points.Length; i++)
        {
            int at = vh.currentVertCount;
            Add(vh, centre, colour);
            Add(vh, points[i], colour);
            Add(vh, points[(i + 1) % points.Length], colour);
            vh.AddTriangle(at, at + 1, at + 2);
        }
    }

    private static void Add(VertexHelper vh, Vector2 position, Color colour)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = colour;
        vh.AddVert(vertex);
    }
}

/// <summary>
/// 技階的記號：一塊黃銅牌，上面刻著羅馬數字。0 = 還沒敘階，牌是空的。
/// </summary>
/// <remarks>
/// **為什麼羅馬數字是畫的不是打的。** 它只有三個字母（I、V、X），而且每一個都是直線
/// —— 畫出來只要十幾個四邊形，卻換到兩件字型給不了的東西：筆畫可以照牌面寬度重新排
/// （VIII 比 X 寬一倍，字型只會把它擠出去），以及<b>刻痕</b>——每一道先畫一份偏右下
/// 的亮邊、再畫深色的溝蓋上去，那是「刻進去」而不是「印上去」。
/// </remarks>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class RomanTabletGraphic : MaskableGraphic
{
    /// <summary>每個字母佔多寬，以字高為單位。</summary>
    private const float WideGlyph = 0.78f;
    private const float NarrowGlyph = 0.30f;
    private const float GlyphGap = 0.13f;
    private const float Stroke = 0.14f;

    [SerializeField] private int numeral;

    public int Numeral
    {
        get => numeral;
        set
        {
            if (numeral == value) return;
            numeral = value;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width < 8f || r.height < 8f) return;

        // 板子：外圈深銅當邊，內縮一圈亮銅當面，再內縮一圈當凹下去的刻字區。
        // 三層：外圈深銅是邊，中層亮銅是面，內縮那一層暗下去是凹進去的刻字區。
        // 上一版三層只差一點點，整塊在 58px 上讀起來是一塊素面的黃銅片。
        float chamfer = Mathf.Min(r.width, r.height) * 0.16f;
        SyllabusIcons.Plate(vh, r, chamfer, SyllabusIcons.BrassDeep);
        Rect face = Inset(r, 3.2f);
        SyllabusIcons.Plate(vh, face, chamfer * 0.80f, SyllabusIcons.Brass);
        Rect field = Inset(face, 3.0f);
        SyllabusIcons.Plate(vh, field, chamfer * 0.62f,
            new Color(SyllabusIcons.Brass.r * 0.54f, SyllabusIcons.Brass.g * 0.50f,
                SyllabusIcons.Brass.b * 0.36f, 1f));

        // 還沒敘階就刻一道橫槓。
        //
        // 本來這裡是直接 return，想法是「空的牌本來就是還沒發生的樣子」—— 那個想法
        // 錯了：一塊什麼都沒有的銅片在畫面上讀起來是<b>圖示沒載入</b>，不是「未敘
        // 階」。刻一道進去，它就變成一塊「還沒刻字」的牌，而那是看得出來的。
        string glyphs = Roman(numeral);
        if (glyphs.Length == 0)
        {
            float bar = field.width * 0.46f;
            float thick = field.height * 0.11f;
            Glyph(vh, '-', field.center.x - bar * 0.5f, field.center.y - thick * 0.5f,
                bar, thick);
            return;
        }

        // 字高由刻字區決定，寬度不夠就整串縮 —— VIII 比 X 寬一倍，固定字高的那一版
        // 會把最後一畫推出板子外面。
        float span = 0f;
        for (int i = 0; i < glyphs.Length; i++)
        {
            span += glyphs[i] == 'I' ? NarrowGlyph : WideGlyph;
            if (i < glyphs.Length - 1) span += GlyphGap;
        }
        float height = Mathf.Min(field.height * 0.74f, field.width * 0.86f / Mathf.Max(span, 0.01f));
        float width = span * height;
        float x = field.center.x - width * 0.5f;
        float bottom = field.center.y - height * 0.5f;

        for (int i = 0; i < glyphs.Length; i++)
        {
            float w = (glyphs[i] == 'I' ? NarrowGlyph : WideGlyph) * height;
            Glyph(vh, glyphs[i], x, bottom, w, height);
            x += w + GlyphGap * height;
        }
    }

    /// <summary>一個字母。先畫偏右下的亮邊，再畫深色的溝蓋上去 —— 那就是刻痕。</summary>
    private void Glyph(VertexHelper vh, char glyph, float x, float y, float w, float h)
    {
        Color lit = new Color(1f, 0.94f, 0.76f, 0.85f);
        // 房間的主光在左上，所以刻痕被照到的是**右下那一面**牆。
        Vector2 shift = new Vector2(h * 0.055f, -h * 0.055f);
        Strokes(vh, glyph, x + shift.x, y + shift.y, w, h, lit);
        Strokes(vh, glyph, x, y, w, h, SyllabusIcons.Engrave);
    }

    private void Strokes(VertexHelper vh, char glyph, float x, float y, float w, float h,
        Color colour)
    {
        float t = Stroke * h;
        float serif = t * 0.72f;
        switch (glyph)
        {
            case 'I':
                SyllabusIcons.Bar(vh, new Vector2(x + w * 0.5f, y), new Vector2(x + w * 0.5f, y + h), t, colour);
                // 襯線：沒有它，I 讀起來是一條分隔線不是一個字母。
                SyllabusIcons.Bar(vh, new Vector2(x, y), new Vector2(x + w, y), serif, colour);
                SyllabusIcons.Bar(vh, new Vector2(x, y + h), new Vector2(x + w, y + h), serif, colour);
                break;
            case 'V':
                SyllabusIcons.Bar(vh, new Vector2(x + t * 0.5f, y + h),
                    new Vector2(x + w * 0.5f, y), t, colour);
                SyllabusIcons.Bar(vh, new Vector2(x + w - t * 0.5f, y + h),
                    new Vector2(x + w * 0.5f, y), t, colour);
                SyllabusIcons.Bar(vh, new Vector2(x - t * 0.2f, y + h),
                    new Vector2(x + t * 1.4f, y + h), serif, colour);
                SyllabusIcons.Bar(vh, new Vector2(x + w - t * 1.4f, y + h),
                    new Vector2(x + w + t * 0.2f, y + h), serif, colour);
                break;
            case 'X':
                SyllabusIcons.Bar(vh, new Vector2(x + t * 0.5f, y + h),
                    new Vector2(x + w - t * 0.5f, y), t, colour);
                SyllabusIcons.Bar(vh, new Vector2(x + w - t * 0.5f, y + h),
                    new Vector2(x + t * 0.5f, y), t, colour);
                break;
            case '-':
                // 空牌上的那一道。h 傳進來就是它的厚度，所以不吃 Stroke。
                SyllabusIcons.Bar(vh, new Vector2(x, y + h * 0.5f),
                    new Vector2(x + w, y + h * 0.5f), h, colour);
                break;
        }
    }

    /// <summary>1..10 的羅馬數字。超出範圍回空字串 —— 空牌就是「還沒敘階」。</summary>
    private static string Roman(int value)
    {
        switch (value)
        {
            case 1: return "I";
            case 2: return "II";
            case 3: return "III";
            case 4: return "IV";
            case 5: return "V";
            case 6: return "VI";
            case 7: return "VII";
            case 8: return "VIII";
            case 9: return "IX";
            case 10: return "X";
            default: return string.Empty;
        }
    }

    private static Rect Inset(Rect r, float by) =>
        new Rect(r.xMin + by, r.yMin + by, r.width - by * 2f, r.height - by * 2f);
}

/// <summary>
/// 檢定的記號：一枚朱紅圓印。中間的數字是級數，外圈的齒和內圈是等第。
/// </summary>
/// <remarks>
/// **為什麼等第畫在圈上而不是寫在旁邊。** 及第／優等／特優是同一枚印的三種版本，不是
/// 三個詞：平圈是及第，外圈加一圈齒是優等，再加一道內圈是特優。所以「幾級、什麼等
/// 第」是<b>一個</b>記號回答的，不是兩件要對照的事。
///
/// **還沒受檢就是一枚空心的印。** 不填朱砂、不刻數字，只有一圈淡淡的輪廓 —— 那是還
/// 沒蓋過的印本來的樣子，比一個灰掉的數字誠實。
/// </remarks>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class GradeSealGraphic : MaskableGraphic
{
    private const int Teeth = 18;

    [SerializeField] private int grade;
    [SerializeField] private int seal;

    /// <summary>級數。0 = 還沒受檢。</summary>
    public int Grade
    {
        get => grade;
        set
        {
            if (grade == value) return;
            grade = value;
            SetVerticesDirty();
        }
    }

    /// <summary>等第：0 無、1 及第、2 優等、3 特優。見 <see cref="SyllabusLadder.SealRank"/>。</summary>
    public int Seal
    {
        get => seal;
        set
        {
            if (seal == value) return;
            seal = value;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float size = Mathf.Min(r.width, r.height);
        if (size < 8f) return;

        Vector2 centre = r.center;
        float radius = size * 0.46f;
        bool stamped = grade > 0;

        if (!stamped)
        {
            // 空印：一圈輪廓加中間一道橫槓。
            //
            // 本來只有一圈細線，而它在畫面上讀起來是<b>圖示沒載入</b>，不是「未受
            // 檢」。加粗那一圈、再刻一道進去，它才是一枚「還沒蓋過」的印。
            SyllabusIcons.Ring(vh, centre, radius * 0.74f, radius * 0.90f,
                SyllabusIcons.VermilionDim);
            float bar = radius * 0.80f;
            float thick = radius * 0.17f;
            SyllabusIcons.Bar(vh, new Vector2(centre.x - bar * 0.5f, centre.y),
                new Vector2(centre.x + bar * 0.5f, centre.y), thick,
                SyllabusIcons.VermilionDim);
            return;
        }

        // 優等以上：外圈一圈齒。印越好，印面越講究 —— 這是同一枚印的三個版本。
        if (seal >= 2)
        {
            for (int i = 0; i < Teeth; i++)
            {
                float a = Mathf.PI * 2f * (i + 0.5f) / Teeth;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 side = new Vector2(-dir.y, dir.x) * radius * 0.085f;
                Vector2 root = centre + dir * radius * 0.90f;
                SyllabusIcons.Quad(vh, root - side, root + side,
                    centre + dir * radius * 1.06f, centre + dir * radius * 1.06f,
                    SyllabusIcons.Vermilion);
            }
        }

        SyllabusIcons.Ring(vh, centre, 0f, radius * 0.94f, SyllabusIcons.Vermilion);
        // 印面內縮一圈留白，數字才不會貼著邊 —— 真的印章都是這樣留的。
        SyllabusIcons.Ring(vh, centre, radius * 0.74f, radius * 0.84f,
            new Color(1f, 0.93f, 0.86f, 0.92f));
        if (seal >= 3)
            SyllabusIcons.Ring(vh, centre, radius * 0.62f, radius * 0.68f,
                new Color(1f, 0.93f, 0.86f, 0.78f));
    }
}
