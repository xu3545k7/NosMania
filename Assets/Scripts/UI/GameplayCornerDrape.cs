using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 遊玩畫面左右兩側的側幕：從頂端垂到底，內緣一路掛著金繩和穗子。
/// </summary>
/// <remarks>
/// **為什麼掛在背景那一層，不是 HUD。** 遊玩的 HUD canvas 是 24000 的
/// ScreenSpaceOverlay，畫在所有東西上面 —— 而音符是從畫面頂端掉下來的，上面擺一塊
/// 布就會把剛生出來的音符遮掉。這是<b>背景的裝飾</b>，所以它跟著背景圖走、排在它後
/// 面一格：蓋得住照片，蓋不到音符。
///
/// **為什麼不從簷幕那張圖上裁一塊。** 試過，不行。裁出來的那一塊，內側和下緣都是
/// 直角的切口 —— 一塊布不會有直角。這裡用的是專門烘的一張<b>左腿</b>
/// （<c>Assets/Resources/UI/GameplayLegDrape.png</c>，產生器
/// <c>scratchpad/bake_leg.py</c>）：內緣是它自己的一條曲線、從上到下整條掛穗，木條
/// 也烘在圖裡。右邊是同一張圖 UV 左右鏡射 —— 兩側本來就該是對稱的。
///
/// **為什麼寬度由高度決定。** 側幕是垂到底的，所以高度一定是滿版；寬度跟著貼圖自己
/// 的長寬比走，任何畫面比例下布的褶都不會被拉扁。
/// </remarks>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class GameplayCornerDrape : MaskableGraphic
{
    /// <summary>烘圖尺寸。換貼圖要同步改。</summary>
    private const float BakeWidth = 620f;
    private const float BakeHeight = 1560f;

    /// <summary>
    /// 上端收到下端的幾成。這是侧幕的透視。
    /// </summary>
    /// <remarks>
    /// **一塊正面的長方形在這個畫面上是錯的。** 背景是
    /// <see cref="Effects.ConcertHallBackdrop"/> —— 從舞台上往觀眾席看的大廳，挑臺、
    /// 側包廂、軌道全部收斂到中央的消失點。畫面上唯獨兩側的布是正面的話，
    /// 它就是貼在房間上面的一張紙，而不是站在房間裡的一塊布。
    ///
    /// 上窄下寬：靠鏡頭的那一端（下）大、往消失點那一端（上）小。網格切成
    /// <see cref="Strips"/> 條橫帶插值，不是一塊四邊形 —— 一塊梯形的四邊形在
    /// UI 裡是仿射插值的，對角線上的貼圖會歪掉。
    /// </remarks>
    private const float SlantTop = 0.74f;

    /// <summary>垂直切幾條。多一點只是頂點，但貼圖不會歪。</summary>
    private const int Strips = 24;

    private static Texture2D cloth;
    private static bool clothLoaded;

    public override Texture mainTexture
    {
        get
        {
            Load();
            return cloth != null ? (Texture)cloth : base.mainTexture;
        }
    }

    /// <summary>掛在背景圖旁邊，排在它後面一格。</summary>
    public static GameplayCornerDrape Attach(RawImage background)
    {
        if (background == null) return null;
        Transform parent = background.transform.parent;
        if (parent == null) return null;
        Transform existing = parent.Find("GameplayCornerDrape");
        if (existing != null) return existing.GetComponent<GameplayCornerDrape>();

        // 大廳也掛在「背景圖後面一格」—— 兩個搶同一個位置。布是台口的東西，
        // 離鏡頭最近，所以要在大廳之上。
        Transform hall = parent.Find("~ConcertHallBackdrop");
        int slot = (hall != null ? hall.GetSiblingIndex() : background.transform.GetSiblingIndex()) + 1;

        var host = new GameObject("GameplayCornerDrape", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(GameplayCornerDrape));
        host.layer = background.gameObject.layer;
        var rect = host.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetSiblingIndex(slot);

        var drape = host.GetComponent<GameplayCornerDrape>();
        drape.raycastTarget = false;
        return drape;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Load();
        Rect r = rectTransform.rect;
        if (cloth == null || r.width < 64f || r.height < 64f) return;

        float width = r.height * (BakeWidth / BakeHeight);
        // 左邊照原圖，右邊 u 反過來讀 —— 一張圖兩側用。
        Leg(vh, r, width, -1f);
        Leg(vh, r, width, +1f);
    }

    /// <summary>一側的側幕。外緣貼著畫面邊，內緣往消失點收。</summary>
    private void Leg(VertexHelper vh, Rect r, float width, float side)
    {
        float outer = side < 0f ? r.xMin : r.xMax;
        // **兩側都是「外緣 u=0、內緣 u=1」。**
        //
        // 鏡射已經由 <c>outer - side * w</c> 做完了：u 再翻一次就等於沒翻，右邊會在
        // 畫面最外側貼上貼圖的**內緣弧**，左右就不對稱了。
        const float uOuter = 0f;
        const float uInner = 1f;

        for (int i = 0; i < Strips; i++)
        {
            float s0 = i / (float)Strips;
            float s1 = (i + 1f) / Strips;
            float y0 = Mathf.Lerp(r.yMax, r.yMin, s0);
            float y1 = Mathf.Lerp(r.yMax, r.yMin, s1);
            float w0 = width * Mathf.Lerp(SlantTop, 1f, s0);
            float w1 = width * Mathf.Lerp(SlantTop, 1f, s1);

            int at = vh.currentVertCount;
            Add(vh, new Vector2(outer, y0), new Vector2(uOuter, 1f - s0));
            Add(vh, new Vector2(outer - side * w0, y0), new Vector2(uInner, 1f - s0));
            Add(vh, new Vector2(outer - side * w1, y1), new Vector2(uInner, 1f - s1));
            Add(vh, new Vector2(outer, y1), new Vector2(uOuter, 1f - s1));
            vh.AddTriangle(at, at + 1, at + 2);
            vh.AddTriangle(at + 2, at + 3, at);
        }
    }

    private void Add(VertexHelper vh, Vector2 position, Vector2 uv)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.uv0 = uv;
        vertex.color = color;
        vh.AddVert(vertex);
    }

    private static void Load()
    {
        if (clothLoaded) return;
        clothLoaded = true;
        cloth = Resources.Load<Texture2D>("UI/GameplayLegDrape");
    }
}
