using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 捲簾的拉繩：一條絞股的細繩，末端一顆車出來的木珠。
/// </summary>
/// <remarks>
/// **為什麼繩子是程序生成的。** 它的長度隨簾子開合一直在變（珠子永遠吊在簾的下緣
/// 底下一點），而一張貼圖不是被拉長就是要九宮格；絞股的斜紋一被縱向拉伸就變成一條
/// 斜線，那是麻花不是繩子。直接依 rect 高度切段，股距就永遠是固定的。
///
/// **為什麼珠子要車出來的形狀。** 一顆圓球讀起來是「一個點」，而車製的木珠有頸、有
/// 腰、有底 —— 三段輪廓一出來，眼睛就知道那是可以捏住往下拉的東西。這條繩平常是畫
/// 面上唯一提示「這裡有東西可以拉」的線索，它必須看得出來是握把。
/// </remarks>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class BlindCordGraphic : MaskableGraphic
{
    /// <summary>繩子本身多寬。</summary>
    private const float CordWidth = 5f;

    /// <summary>一股多長。段數由 rect 高度除這個算出來，所以股距不隨長度變。</summary>
    private const float StrandPitch = 9f;

    /// <summary>木珠的高與最寬處。</summary>
    private const float BeadHeight = 30f;
    private const float BeadWidth = 17f;

    // 兩套顏色：練習室是白樺房間，繩跟著走淡米白；演奏會是金。
    private static readonly Color[] CordLit =
        { new Color(0.930f, 0.902f, 0.838f, 1f), new Color(0.898f, 0.812f, 0.560f, 1f) };
    private static readonly Color[] CordDim =
        { new Color(0.680f, 0.630f, 0.548f, 1f), new Color(0.612f, 0.502f, 0.268f, 1f) };
    private static readonly Color[] BeadLit =
        { new Color(0.952f, 0.930f, 0.880f, 1f), new Color(0.945f, 0.855f, 0.585f, 1f) };
    private static readonly Color[] BeadMid =
        { new Color(0.782f, 0.742f, 0.672f, 1f), new Color(0.712f, 0.565f, 0.292f, 1f) };
    private static readonly Color[] BeadDeep =
        { new Color(0.470f, 0.428f, 0.362f, 1f), new Color(0.392f, 0.282f, 0.128f, 1f) };

    [SerializeField] private bool brass;

    /// <summary>金的還是白的。演奏會用金，練習室用白樺。</summary>
    public bool Brass
    {
        get => brass;
        set
        {
            if (brass == value) return;
            brass = value;
            SetVerticesDirty();
        }
    }

    /// <summary>珠子底緣離 rect 底緣留多少，讓它看起來是吊著而不是撐在底上。</summary>
    public static float BeadReach => BeadHeight + 6f;

    /// <summary>拉一下的時候繩往下走多少。</summary>
    private const float TugDepth = 26f;
    /// <summary>一次拉扯多久（下去加上來）。</summary>
    private const float TugSpan = 0.34f;

    private float tug = -1f;

    /// <summary>
    /// 拉一下：繩往下跑一段再彈回來。
    /// </summary>
    /// <remarks>
    /// **繩的長度本身不變。** 上一版是繩跟著布的高度一起伸縮 —— 那讀起來是繩
    /// 被布拉走了，而真實的捲簾是反過來：繩永遠那麼長，你拉它一下，它彈回去，
    /// 動的是布。所以這裡只把網格往下延，rect 不動 —— 繩沒有遮罩，畫到框外面
    /// 本來就沒問題。
    /// </remarks>
    public void Tug()
    {
        tug = 0f;
        SetVerticesDirty();
    }

    private float TugOffset => tug < 0f
        ? 0f
        : Mathf.Sin(Mathf.PI * Mathf.Clamp01(tug / TugSpan)) * TugDepth;

    private void Update()
    {
        if (tug < 0f) return;
        tug += Time.unscaledDeltaTime;
        if (tug >= TugSpan) tug = -1f;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.height < BeadHeight + 8f || r.width < BeadWidth) return;

        int k = brass ? 1 : 0;
        float cx = r.center.x;
        float top = r.yMax;
        // 拉扯的時候繩往下延，rect 不動。
        float bottom = r.yMin - TugOffset;
        float beadTop = bottom + BeadHeight;

        // 繩身：一股亮一股暗，交界處收窄一點，讀起來才是絞起來的。
        int strands = Mathf.Max(1, Mathf.RoundToInt((top - beadTop) / StrandPitch));
        float pitch = (top - beadTop) / strands;
        for (int i = 0; i < strands; i++)
        {
            float y0 = top - pitch * i;
            float y1 = top - pitch * (i + 1);
            float ym = (y0 + y1) * 0.5f;
            Color a = (i % 2 == 0) ? CordLit[k] : CordDim[k];
            Color b = (i % 2 == 0) ? CordDim[k] : CordLit[k];
            float wide = CordWidth * 0.5f;
            float thin = CordWidth * 0.34f;
            Quad(vh, new Vector2(cx - thin, y0), new Vector2(cx + thin, y0),
                new Vector2(cx + wide, ym), new Vector2(cx - wide, ym), a, a, a, a);
            Quad(vh, new Vector2(cx - wide, ym), new Vector2(cx + wide, ym),
                new Vector2(cx + thin, y1), new Vector2(cx - thin, y1), b, b, b, b);
        }

        // 木珠：頸、腰、底。輪廓用五段折線近似，比畫一個圓誠實 —— 它是車出來的。
        float half = BeadWidth * 0.5f;
        float[] profile = { 0.26f, 0.62f, 1.00f, 0.92f, 0.44f, 0.20f };
        for (int i = 0; i < profile.Length - 1; i++)
        {
            float t0 = i / (float)(profile.Length - 1);
            float t1 = (i + 1) / (float)(profile.Length - 1);
            float y0 = Mathf.Lerp(beadTop, bottom, t0);
            float y1 = Mathf.Lerp(beadTop, bottom, t1);
            float w0 = half * profile[i];
            float w1 = half * profile[i + 1];
            // 左半受光、右半收進暗處：光和整個房間一樣從左上來。
            Color lit0 = Color.Lerp(BeadLit[k], BeadMid[k], t0);
            Color lit1 = Color.Lerp(BeadLit[k], BeadMid[k], t1);
            Color dim0 = Color.Lerp(BeadMid[k], BeadDeep[k], t0);
            Color dim1 = Color.Lerp(BeadMid[k], BeadDeep[k], t1);
            Quad(vh, new Vector2(cx - w0, y0), new Vector2(cx, y0),
                new Vector2(cx, y1), new Vector2(cx - w1, y1), lit0, lit0, lit1, lit1);
            Quad(vh, new Vector2(cx, y0), new Vector2(cx + w0, y0),
                new Vector2(cx + w1, y1), new Vector2(cx, y1), lit0, dim0, dim1, lit1);
        }
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
