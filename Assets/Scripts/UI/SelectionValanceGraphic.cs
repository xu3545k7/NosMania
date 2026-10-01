using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 選曲畫面頂端的台口簷幕：掛在簾桿底下的一張烘好的貼圖。
/// </summary>
/// <remarks>
/// **為什麼從程序網格換成貼圖。** 上一版是當場算出來的網格 —— 抽褶頂帶、五片互相
/// 交疊的垂幔、兩個角落扇、金邊、垂飾。它在離線的白底測試上量到的內部破洞是 0，在
/// 八種畫布尺寸下逐欄掃也是 0，但實機上就是會看到後面。原因有三個，而且每一個都只
/// 在「畫到螢幕上」的那一刻才出現：
///
/// <list type="number">
/// <item>金邊只有 <c>0.019 × unit</c> ≈ 2.8 單位寬，換算到螢幕不到 1.3 px。逐段
/// 畫的時候有的段捨進成 1 px、有的捨成 0 px，整條金線斷成虛線，夾著雜點。</item>
/// <item>下襬的切口和投影是 alpha 0.62 的半透明四邊形。兩片垂幔交疊的地方，上面
/// 那片的投影蓋在底下那片的布上 —— 半透明的東西疊在布上，就是「看得到底下那片」，
/// 而且它有一條硬邊，讀起來是把輪廓畫在布上面。</item>
/// <item>它住在氛圍包裡，sibling index 幾乎在最底，所以場景裡的曲目卡整張畫在布
/// 前面，中央被挖出一塊直角矩形。</item>
/// </list>
///
/// 這三件事各自都能修，但它們會輪流出現，而且每一次都長得像「布是透明的」。烘成一
/// 張圖之後三個都不存在：四倍超取樣所以沒有次像素的線，輪廓內 alpha 一律 255 所以
/// 沒有任何東西可以疊透，而且它只剩一個四邊形，排序只要處理一次。
///
/// **貼圖怎麼來的。** <c>scratchpad/bake_valance.py</c>，幾何和這裡的常數同源。要
/// 改簾幕的形狀就改那支再跑一次，輸出直接蓋掉
/// <c>Assets/Resources/UI/ProsceniumValance.png</c>。切口與投影在烘的時候只畫在背
/// 後沒有布的地方，所以內部的接縫只剩一條金線。
///
/// **為什麼橫向拉伸、縱向照 unit 縮。** 布一定要橫跨整個畫面寬，而畫面寬會隨比例
/// 變（16:9 的邏輯畫布 2560、5:4 只有 2147）。橫向拉伸對垂幔看不出來 —— 那本來就
/// 是一塊被抓出褶的布。縱向如果也跟著寬度縮，寬螢幕上簾幕會變得很深，把曲目卡吃
/// 掉，所以縱向綁 <c>unit</c>，深度在任何比例下都和設計時一樣。
/// </remarks>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class SelectionValanceGraphic : MaskableGraphic
{
    /// <summary>烘圖時用的 unit。貼圖的每一個像素都是照這個尺度算出來的。</summary>
    private const float BakeUnit = 146.88f;

    /// <summary>烘圖的高，邏輯單位。</summary>
    /// <remarks>
    /// 寫成常數而不是讀 <c>cloth.height</c>：Unity 的匯入設定（NPOT 縮放、
    /// maxTextureSize）隨時可能把貼圖縮成別的尺寸，而 UV 是 0..1，所以形狀不該
    /// 跟著像素數走。改烘圖腳本的尺寸時要連這一行一起改。
    /// </remarks>
    private const float BakeHeight = 828f;

    /// <summary>
    /// 布的上緣往上塞進簾桿底下多少，unit 的倍數。
    /// </summary>
    /// <remarks>
    /// **不是為了省空間，是為了藏提遇點。** 每一片垂幔在它兩個抓點上收束，
    /// 那兩個點露在外面的話，每一片都讀成一個完整的 U —— 七個磗排一排。參考
    /// 圖的布在畫面頂端被切掉，所以只看得到下半截的弧，互相疊著—— 那才是一
    /// 塊布。1.14 剛好把抓點（深度 143）推到桿底下；1.71 是使用者在那之後又往上推了一成五。
    ///
    /// 實作上是剪貼圖而不是把它畫到桿後面：簷幕排在曲目卡之上，而桿在更底
    /// 層（見 <c>SongSelectionManager</c> 的順序），直接往上移會反過來蓋住桿。剪掉上
    /// 緣那一段 UV 的結果完全一樣，而且不用動排序。
    /// </remarks>
    private const float TuckUnits = 1.71f;

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

    /// <summary>
    /// 簾幕從 rect 頂緣往下遮到哪裡（最淺的那一點，在中央）。
    /// </summary>
    /// <remarks>
    /// 燈拿這個把自己在布後面那一段關掉（見 <c>SelectionLightMask</c>）。一盞在布
    /// 後面的燈，被布擋住的那一段本來就不該存在 —— 這比去調 sibling order 可靠，因
    /// 為不管誰把它畫在布上面，都沒有東西可以漏。
    ///
    /// 取中央的深度（最淺）而不是邊緣的：寧可讓光從扇貝的谷底早一點凸出來，也不要
    /// 把一整段本來看得到的光餐掉。
    /// </remarks>
    public static float CoverDepth(Rect r)
    {
        if (r.width < 64f || r.height < 64f) return 0f;
        // 只算簾桿。
        //
        // 上一版還加了抽褶帶和垂幔的深度，那是在布還是一整條連續的時候。塞進
        // 桿底下之後，兩片垂幔之間的缺口就在桿的正下方 —— 廣燈和聚光該從那裡就
        // 看得到，多餐一段反而是錯的。而布本身現在排在所有燈之上，所以這個閘
        // 門只是省掉永遠看不到的幾何，不再是防漏的那一道防線。
        return r.height * (0.01f / 1.02f) + SelectionRailGraphic.Height;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Load();
        Rect r = rectTransform.rect;
        if (cloth == null || r.width < 64f || r.height < 64f) return;

        // 布掛在簾桿底下，不是掛在 rect 的頂緣。rect 比畫布高出 1% × height（氛圍
        // 根的 2% 外擴），所以要先扣掉那一段，再扣桿高。少扣任何一段，抽褶帶都會從
        // 桿的中間長出來。
        float top = r.yMax - (r.height * (0.01f / 1.02f) + SelectionRailGraphic.Height);
        float unit = Mathf.Min(r.width, r.height) * 0.10f;
        // 上緣剪掉 TuckUnits：畫得到的只剩貼圖的下半截。
        float tuck = Mathf.Clamp(TuckUnits * BakeUnit, 0f, BakeHeight - 1f);
        float height = (BakeHeight - tuck) * (unit / BakeUnit);
        float vTop = 1f - tuck / BakeHeight;

        Vector2 p0 = new Vector2(r.xMin, top);
        Vector2 p1 = new Vector2(r.xMax, top);
        Vector2 p2 = new Vector2(r.xMax, top - height);
        Vector2 p3 = new Vector2(r.xMin, top - height);

        int at = vh.currentVertCount;
        Add(vh, p0, new Vector2(0f, vTop));
        Add(vh, p1, new Vector2(1f, vTop));
        Add(vh, p2, new Vector2(1f, 0f));
        Add(vh, p3, new Vector2(0f, 0f));
        vh.AddTriangle(at, at + 1, at + 2);
        vh.AddTriangle(at + 2, at + 3, at);
    }

    private void Add(VertexHelper vh, Vector2 position, Vector2 uv)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vertex.uv0 = uv;
        vh.AddVert(vertex);
    }

    /// <summary>
    /// 貼圖只載一次，整個 process 共用。
    /// </summary>
    /// <remarks>
    /// 找不到就什麼都不畫，而且不吵。少一片裝飾不值得換一個壞掉的畫面，而缺檔在
    /// Editor 裡一眼就看得出來。
    /// </remarks>
    private static void Load()
    {
        if (clothLoaded) return;
        clothLoaded = true;
        cloth = Resources.Load<Texture2D>("UI/ProsceniumValance");
    }
}
