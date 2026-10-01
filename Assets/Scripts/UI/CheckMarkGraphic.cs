using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一個小勾勾。兩筆：短的往下，長的往上。
/// </summary>
/// <remarks>
/// 和 <see cref="FunnelIconGraphic"/> 同一個理由：不用「✓」字元。這個專案的字型會隨
/// 語言切換，而符號字在不同字型裡的粗細和基線都不一樣 —— 一個只有 16 見方的記號禁
/// 不起那種差異。
///
/// 兩筆的轉折處是**同一組頂點**，不是兩個各自的矩形對在一起：沒有 MSAA 的時候，兩
/// 個端點對接會留下一個看得見的缺口。
/// </remarks>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CheckMarkGraphic : MaskableGraphic
{
    /// <summary>筆畫寬，佔 rect 短邊的幾成。</summary>
    private const float StrokeShare = 0.19f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width < 4f || r.height < 4f) return;

        float w = Mathf.Min(r.width, r.height) * StrokeShare;
        // 三個轉折點：起筆、谷底、收筆。
        Vector2 a = new Vector2(r.xMin + r.width * 0.08f, r.yMin + r.height * 0.52f);
        Vector2 b = new Vector2(r.xMin + r.width * 0.38f, r.yMin + r.height * 0.16f);
        Vector2 c = new Vector2(r.xMin + r.width * 0.94f, r.yMin + r.height * 0.86f);

        Stroke(vh, a, b, w);
        Stroke(vh, b, c, w);
        // 轉折處補一顆方塊，兩筆才連得起來。
        Quad(vh, b + new Vector2(-w * 0.5f, -w * 0.5f), b + new Vector2(w * 0.5f, -w * 0.5f),
            b + new Vector2(w * 0.5f, w * 0.5f), b + new Vector2(-w * 0.5f, w * 0.5f));
    }

    private void Stroke(VertexHelper vh, Vector2 from, Vector2 to, float width)
    {
        Vector2 along = (to - from).normalized;
        Vector2 across = new Vector2(-along.y, along.x) * (width * 0.5f);
        Quad(vh, from - across, from + across, to + across, to - across);
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
