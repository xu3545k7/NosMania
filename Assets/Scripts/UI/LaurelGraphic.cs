using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全連的記號：一圈金色的月桂，兩束葉子從底下往上抱。
/// </summary>
/// <remarks>
/// **為什麼是月桂不是星星。** 星星是現代手遊的語言，而這間房裡沒有第二顆星；月桂
/// 是「這一首拿過獎」，而且它和旁邊的名次勳章不會撞 —— 一個是**環**、一個是實心的
/// 圓牌，即使縮到 40px 兩者的剪影還是分得開。火漆印則和玩家資料那枚印重複了語彙。
///
/// **為什麼葉子是四邊形不是曲線。** 這個記號在清單裡只有 40px。在那個尺寸上，貝茲
/// 曲線和一個帶尖的四邊形畫出來一模一樣，而四邊形只要四個頂點。整個記號是六片葉加
/// 兩條莖，72 個頂點。
///
/// **為什麼兩束不對稱。** 真的月桂冠是兩束綁在一起的，兩邊的葉子不會剛好對齊。右邊
/// 那束從 <see cref="Phase"/> 偏半片葉的距離 —— 完全鏡像的那一版讀起來是一個印出來
/// 的符號，不是兩束編起來的東西。
/// </remarks>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class LaurelGraphic : MaskableGraphic
{
    /// <summary>每束幾片葉。</summary>
    /// <remarks>
    /// **3，不是 7。** 這個記號只有 40px，七片葉一片就剩 3 像素 —— 那是解析度的物理
    /// 限制，不是參數問題，倍率再怎麼調都救不回來（實測 36 和 40 都一樣糊）。少而大
    /// 的葉子才撐得住：三片各約 12px，一眼就看得出是葉子。
    /// </remarks>
    private const int LeavesPerSide = 3;

    /// <summary>葉子相對於莖的傾角。0 是直直朝外，90 是完全沿著莖往上。</summary>
    /// <remarks>
    /// **78，不是 38。** 38 的時候葉子直直向外放射，整個記號讀起來是**太陽**不是月
    /// 桂 —— 真的月桂葉幾乎是貼著莖往上長的。這是這個圖形最關鍵的一個數字。
    /// </remarks>
    private const float LeafLean = 78f;

    /// <summary>兩束之間錯開多少片葉子。0.5 = 半片。</summary>
    private const float Phase = 0.5f;

    /// <summary>缺口：頂端留多少度不畫，冠才是開的。</summary>
    private const float Gap = 60f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float size = Mathf.Min(r.width, r.height);
        if (size <= 6f) return;

        Vector2 centre = r.center;
        float radius = size * 0.38f;
        float stem = Mathf.Max(1f, size * 0.038f);
        float leafLength = size * 0.48f;
        float leafWidth = size * 0.17f;

        for (int side = -1; side <= 1; side += 2)
        {
            // 從底部（180°）往上掃到頂端的缺口。side = −1 走左邊、+1 走右邊。
            float from = 180f - Gap * 0.5f;
            float to = Gap * 0.5f;
            float offset = side < 0 ? 0f : Phase;

            // 莖：一段沿著圓弧的細帶。
            const int stemSteps = 12;
            for (int i = 0; i < stemSteps; i++)
            {
                float a0 = Mathf.Deg2Rad * (90f + side * Mathf.Lerp(from, to, i / (float)stemSteps));
                float a1 = Mathf.Deg2Rad * (90f + side * Mathf.Lerp(from, to, (i + 1) / (float)stemSteps));
                Vector2 p0 = centre + new Vector2(Mathf.Sin(a0), -Mathf.Cos(a0)) * radius;
                Vector2 p1 = centre + new Vector2(Mathf.Sin(a1), -Mathf.Cos(a1)) * radius;
                Vector2 n0 = (p0 - centre).normalized * stem * 0.5f;
                Vector2 n1 = (p1 - centre).normalized * stem * 0.5f;
                Quad(vh, p0 - n0, p1 - n1, p1 + n1, p0 + n0, color);
            }

            for (int i = 0; i < LeavesPerSide; i++)
            {
                float t = (i + 0.5f + offset) / (LeavesPerSide + 0.35f);
                if (t >= 1f) continue;
                float deg = Mathf.Lerp(from, to, t);
                float a = Mathf.Deg2Rad * (90f + side * deg);
                Vector2 on = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));     // 向外
                Vector2 along = new Vector2(on.y, -on.x) * side;           // 沿著莖往上
                Vector2 root = centre + on * radius;

                // 葉子朝外並且往上傾：越靠近頂端的葉子越立起來，那是編冠的樣子。
                float lean = Mathf.Deg2Rad * LeafLean;
                Vector2 dir = (on * Mathf.Cos(lean) + along * Mathf.Sin(lean)).normalized;
                Vector2 side2 = new Vector2(-dir.y, dir.x);
                float shrink = Mathf.Lerp(1f, 0.72f, t);                   // 尖端的葉子小一點
                Vector2 tip = root + dir * leafLength * shrink;
                Vector2 belly = root + dir * leafLength * 0.38f * shrink;

                // 一片葉子＝根、兩側的腹、尖。四個頂點就夠了：7px 長的葉子上，貝茲和
                // 帶尖的四邊形畫出來一樣。
                Quad(vh, root, belly + side2 * leafWidth * 0.5f * shrink,
                    tip, belly - side2 * leafWidth * 0.5f * shrink, color);
            }
        }
    }

    private static void Quad(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color colour)
    {
        int index = vh.currentVertCount;
        Add(vh, p0, colour);
        Add(vh, p1, colour);
        Add(vh, p2, colour);
        Add(vh, p3, colour);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index + 2, index + 3, index);
    }

    private static void Add(VertexHelper vh, Vector2 position, Color colour)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = colour;
        vh.AddVert(vertex);
    }
}
