using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 漏斗（篩選）圖示，和一個小勾勾。兩個都是程序生成的形狀。
/// </summary>
/// <remarks>
/// **為什麼不用字元。** 「▼」「✓」這種符號在不同字型裡的大小、基線和留白都不一樣，
/// 而這個專案的字型會隨語言切換（見 <c>ApplyLocalizedFont</c>）—— 換一支字型，圖示
/// 就跑位或變成缺字方塊。形狀自己畫的話，尺寸只由 rect 決定。
/// </remarks>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class FunnelIconGraphic : MaskableGraphic
{
    /// <summary>漏斗口佔寬度的幾成。</summary>
    private const float MouthShare = 0.92f;
    /// <summary>漏斗頸佔寬度的幾成。</summary>
    private const float NeckShare = 0.20f;
    /// <summary>斗身佔高度的幾成，剩下的是柄。</summary>
    private const float BowlShare = 0.52f;
    /// <summary>板厚。</summary>
    private const float Stroke = 2.6f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width < 6f || r.height < 6f) return;

        float cx = r.center.x;
        float mouth = r.width * MouthShare * 0.5f;
        float neck = r.width * NeckShare * 0.5f;
        float top = r.yMax;
        float waist = r.yMax - r.height * BowlShare;

        // 斗：上寬下窄的實心梯形。實心而不是描邊 —— 這顆鍵只有 52 見方，
        // 空心的漏斗在這個尺寸上只會讀成一團線。
        Quad(vh, new Vector2(cx - mouth, top), new Vector2(cx + mouth, top),
            new Vector2(cx + neck, waist), new Vector2(cx - neck, waist));

        // 柄：從腰往下一條窄的。
        float stem = Mathf.Min(neck, Stroke * 1.6f);
        Quad(vh, new Vector2(cx - stem, waist), new Vector2(cx + stem, waist),
            new Vector2(cx + stem, r.yMin), new Vector2(cx - stem, r.yMin));
    }

    private void Quad(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        int at = vh.currentVertCount;
        Add(vh, p0);
        Add(vh, p1);
        Add(vh, p2);
        Add(vh, p3);
        vh.AddTriangle(at, at + 1, at + 2);
        vh.AddTriangle(at + 2, at + 3, at);
    }

    private void Add(VertexHelper vh, Vector2 position)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vh.AddVert(vertex);
    }
}
