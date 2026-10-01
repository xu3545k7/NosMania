using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 收合鍵上的箭頭：一個實心三角形，往上收或往下開。
/// </summary>
/// <remarks>
/// **為什麼不用兩條旋轉的細線拼。** 拼出來的 V 在轉角處是兩個矩形的端點對在一起，
/// 沒有 MSAA 的時候那個接縫看得出來，而且線寬要跟著畫面縮放調。一個三角形只有三個
/// 頂點，任何尺寸都乾淨。
///
/// **為什麼不用字元。** 「▲」在不同字型裡的大小、基線和留白都不一樣，而這顆鍵旁邊
/// 就是暫停鍵——兩顆的視覺重量要對得上，交給字型決定就對不上了。
/// </remarks>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CollapseArrowGraphic : MaskableGraphic
{
    [SerializeField] private bool pointsDown;

    /// <summary>往下（展開）還是往上（收合）。</summary>
    public bool PointsDown
    {
        get => pointsDown;
        set
        {
            if (pointsDown == value) return;
            pointsDown = value;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width < 4f || r.height < 4f) return;

        // 三角形比外框窄一些：箭頭要留白，貼著邊的三角形看起來是一塊色塊。
        float halfW = r.width * 0.34f;
        float halfH = r.height * 0.22f;
        float cx = r.center.x;
        float cy = r.center.y;

        Vector2 tip = new Vector2(cx, pointsDown ? cy - halfH : cy + halfH);
        Vector2 left = new Vector2(cx - halfW, pointsDown ? cy + halfH : cy - halfH);
        Vector2 right = new Vector2(cx + halfW, pointsDown ? cy + halfH : cy - halfH);

        int at = vh.currentVertCount;
        Add(vh, left);
        Add(vh, right);
        Add(vh, tip);
        vh.AddTriangle(at, at + 1, at + 2);
    }

    private void Add(VertexHelper vh, Vector2 position)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vh.AddVert(vertex);
    }
}
