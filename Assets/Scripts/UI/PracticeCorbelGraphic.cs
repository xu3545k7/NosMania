using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 頂欄底下掛的一塊托木：上寬下窄的梯形，鋪著同一張白樺紋。
/// </summary>
/// <remarks>
/// **為什麼要自己畫網格。** `Image` 只畫得出矩形，而一塊平白凸出來的方塊讀起來是
/// 「有人把一個長方形貼在板子上」，不是「這片板子中間本來就比較厚」。托木是收邊的
/// 語言：兩道斜肩把重量交還給上面那條橫板。
///
/// **為什麼不能用 `Silhouette.Ramp` 做羽化。** 它用的是 `UIVertex.simpleVert`，uv0
/// 是 (0,0) —— 這個元件有 `mainTexture`，所以整條裙擺都會去取貼圖左下角那一個 texel
/// 的顏色，邊緣會出現一圈和面不同色的流蘇。這裡的每一個頂點都自己算 uv。
///
/// **為什麼上緣不畫接縫。** 上緣接的是頂欄本身，同一塊板子中間不該有線。三條邊
/// （兩道斜肩和底）才是它和牆的交界，用和頂欄底下同一條暗線 —— 房間裡「這塊板子
/// 在那面牆前面」就是這樣講的。
///
/// 專案沒開 MSAA（`PC_RPAsset` 的 `m_MSAA: 1`），而斜肩是這間房唯一的斜邊，硬邊會
/// 一路爬階梯，所以實心收進半格、外面補一圈 alpha 收到 0 的裙擺。
/// </remarks>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class PracticeCorbelGraphic : MaskableGraphic
{
    private const float SeamWidth = 3f;

    [SerializeField] private Texture grain;
    [SerializeField] private float tile = 256f;
    [SerializeField] private float inset = 56f;      // 下緣兩側各內縮多少
    [SerializeField] private Color seam = new Color(0.020f, 0.011f, 0.004f, 0.90f);

    public override Texture mainTexture => grain != null ? grain : base.mainTexture;

    public void Set(Texture texture, float tilePixels, float bottomInset, Color face, Color edge)
    {
        grain = texture;
        tile = Mathf.Max(1f, tilePixels);
        inset = bottomInset;
        seam = edge;
        color = face;
        SetMaterialDirty();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= 8f || r.height <= 4f) return;

        float cut = Mathf.Min(inset, r.width * 0.45f);
        // 0 左上、1 右上、2 右下、3 左下。斜肩是 1→2 和 3→0，底是 2→3。
        Vector2[] ring =
        {
            new Vector2(r.xMin, r.yMax),
            new Vector2(r.xMax, r.yMax),
            new Vector2(r.xMax - cut, r.yMin),
            new Vector2(r.xMin + cut, r.yMin)
        };
        Vector2 centre = r.center;
        Vector2[] solid = Silhouette.Offset(ring, centre, -0.5f);
        Vector2[] skirt = Silhouette.Offset(ring, centre, 0.5f);
        Vector2[] band = Silhouette.Offset(ring, centre, -SeamWidth);

        // 面
        int first = vh.currentVertCount;
        AddVertex(vh, centre, color, r);
        for (int i = 0; i < solid.Length; i++) AddVertex(vh, solid[i], color, r);
        for (int i = 0; i < solid.Length; i++)
            vh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % solid.Length);

        // 裙擺。有接縫的那三條邊要用接縫色收出去，否則最外那一格是亮的木色，讀起來
        // 是「暗線外面還有一條亮邊」。
        for (int i = 0; i < solid.Length; i++)
        {
            int next = (i + 1) % solid.Length;
            Color lip = i == 0 ? color : seam;
            Color clear = new Color(lip.r, lip.g, lip.b, 0f);
            Quad(vh, solid[i], solid[next], skirt[next], skirt[i], lip, lip, clear, clear, r);
        }

        // 接縫：兩道斜肩和底。上緣（0→1）不畫。
        for (int i = 1; i < 4; i++)
        {
            int next = (i + 1) % 4;
            Quad(vh, solid[i], solid[next], band[next], band[i], seam, seam, seam, seam, r);
        }
    }

    private void Quad(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
        Color c0, Color c1, Color c2, Color c3, Rect r)
    {
        int index = vh.currentVertCount;
        AddVertex(vh, p0, c0, r);
        AddVertex(vh, p1, c1, r);
        AddVertex(vh, p2, c2, r);
        AddVertex(vh, p3, c3, r);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index + 2, index + 3, index);
    }

    /// <summary>uv 照像素平鋪，所以托木上的木紋和頂欄是連著的同一張。</summary>
    private void AddVertex(VertexHelper vh, Vector2 position, Color colour, Rect r)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = colour;
        vertex.uv0 = new Vector2((position.x - r.xMin) / tile, (position.y - r.yMin) / tile);
        vh.AddVert(vertex);
    }
}
