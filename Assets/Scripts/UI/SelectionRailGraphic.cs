using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 選曲畫面最上面那條黃銅簾桿：簷幕掛在它下面，控制項鑲在它上面。
/// </summary>
/// <remarks>
/// **為什麼桿在簾上面。** 上一版把它畫在簷幕底下，那是說不通的 —— 簾幕是掛在桿上
/// 的，桿在簾下就變成一塊橫在布前面的板子，而且它會把剛做好的垂幔整條蓋掉。改成桿
/// 在最頂、簾從它底下垂出來之後，兩件東西的關係才是對的，而且桿自然就成了整個畫面
/// 的 nav bar —— 它不是借來的 UI 元件，是這個場景本來就該有的零件。
///
/// **為什麼要一條線腳。** 一條素的銅帶讀起來是「UI 的背景」，不是家具。底緣那一條
/// 鎏金菱格帶加圓花是古典簾桿真的有的線腳，它讓這條桿屬於這間房，而不是壓在房間上
/// 面。圓花同時當分隔：控制項落在兩枚圓花之間，位置就有了理由。
///
/// **為什麼是程序生成不是貼圖。** 桿要橫跨整個畫面寬，而畫面寬會隨比例變
/// （16:9 的畫布 2560、5:4 只有 2147）。貼圖不是被拉伸就是要九宮格，而九宮格切不出
/// 「菱格照固定間距重複、圓花照等分落點」這兩件同時成立的事。
/// </remarks>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class SelectionRailGraphic : MaskableGraphic
{
    /// <summary>桿高。要放得下和練習室同規格的 880×108 評分卡，加上線腳。</summary>
    public const float Height = 160f;

    /// <summary>底緣那條木條的高。</summary>
    private const float InlayHeight = 18f;

    private static readonly Color BodyTop = new Color(0.470f, 0.345f, 0.180f, 1f);
    private static readonly Color BodyBottom = new Color(0.376f, 0.267f, 0.137f, 1f);
    private static readonly Color Lit = new Color(0.659f, 0.510f, 0.275f, 1f);
    private static readonly Color Deep = new Color(0.173f, 0.114f, 0.055f, 1f);
    private static readonly Color Lip = new Color(0.063f, 0.039f, 0.020f, 1f);
    /// <summary>底緣那條木條：深胡桃。桿上唯一不是黃銅的東西。</summary>
    private static readonly Color InlayTop = new Color(0.157f, 0.098f, 0.055f, 1f);
    private static readonly Color InlayBottom = new Color(0.086f, 0.051f, 0.027f, 1f);
    /// <summary>木條上緣的一條受光線。沒有它，木條讀起來是一道陰影不是一塊木頭。</summary>
    private static readonly Color InlayLit = new Color(0.310f, 0.212f, 0.118f, 1f);

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width < 32f || r.height < 16f) return;

        float top = r.yMax;
        float bottom = r.yMin;
        // 桿左右各多長一點：畫布邊緣不該看到桿的斷面。
        float x0 = r.xMin - 40f;
        float x1 = r.xMax + 40f;

        // 本體：上緣受光、往下漸暗，底下一道深色的唇和一條接縫。
        Band(vh, x0, x1, top, top - 4f, Lit, Lit);
        Band(vh, x0, x1, top - 4f, bottom + 10f, BodyTop, BodyBottom);
        Band(vh, x0, x1, bottom + 10f, bottom + 2f, Deep, Deep);
        Band(vh, x0, x1, bottom + 2f, bottom, Lip, Lip);

        // 線腳：底緣一條深色木條。
        //
        // 上一版是鎏金的菱格帶加十一枚圓花。它本身沒做壞，但這條桿已經是整個畫面最
        // 上面那一道橫線，而它底下就是一整片鎏金收邊的簾幕 —— 兩道金線上下相疊，眼
        // 睛分不出哪一條是桿、哪一條是布。換成一條木頭，桿就退回它該有的位置：一件
        // 撐著東西的家具，不是另一件裝飾。
        float inlayTop = bottom + 10f + InlayHeight;
        Band(vh, x0, x1, inlayTop, inlayTop - 2f, InlayLit, InlayLit);
        Band(vh, x0, x1, inlayTop - 2f, bottom + 10f, InlayTop, InlayBottom);
    }

    /// <summary>一條從上到下的橫帶，上下各自一個顏色。</summary>
    private static void Band(VertexHelper vh, float x0, float x1, float yTop, float yBottom,
        Color top, Color bottom)
    {
        Quad(vh, new Vector2(x0, yTop), new Vector2(x1, yTop),
            new Vector2(x1, yBottom), new Vector2(x0, yBottom), top, top, bottom, bottom);
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
