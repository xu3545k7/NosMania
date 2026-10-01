using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 曲繪外面那一圈金邊：貼著**畫實際被畫出來的大小**，不是貼著它的框。
/// </summary>
/// <remarks>
/// **為什麼不能用 uGUI 的 `Outline`。** 它是把整份網格平移之後再畫四份，而那四份
/// 沿用原本的 UV —— `UI/Default` 是 `tex2D(_MainTex, uv) * color`，所以「金邊」畫
/// 出來其實是**封面自己的縮圖**乘上金色：每一首歌的邊看起來亮度都不一樣，深色封
/// 面幾乎沒有邊，帶透明角的封面邊會破洞。
///
/// **為什麼要自己算大小。** 曲繪是 `preserveAspect`，所以它在自己的框裡是置中留
/// 白的；照框去描，邊會浮在畫的外面。這裡照 sprite 的長寬比重算一次，貼著畫的四
/// 邊走。
/// </remarks>
internal sealed class CoverEdgeFit : UIBehaviour
{
    private static readonly Color Lit = new Color(0.92f, 0.78f, 0.44f, 1f);
    private static readonly Color Shade = new Color(0.92f * 0.45f, 0.78f * 0.45f, 0.44f * 0.45f, 1f);

    private Image cover;
    private EdgeLightGraphic edge;
    private RectTransform rect;
    private RectTransform box;
    private Sprite lastSprite;
    private Vector2 lastBox;
    private bool lastEnabled;

    public void Bind(Image coverImage, EdgeLightGraphic ring)
    {
        cover = coverImage;
        edge = ring;
        rect = transform as RectTransform;
        box = coverImage != null ? coverImage.rectTransform : null;
        if (edge != null)
        {
            edge.Thickness = 3f;
            edge.Radius = 0f;
            // 光從左上來，和黃銅牌、書籤的線腳同一個方向。
            edge.SetSides(new[] { Lit, Lit, Shade, Shade });
        }
        lastSprite = null;
        Apply();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        Apply();
    }

    private void Update()
    {
        if (cover == null || box == null) return;
        // 也要看 enabled：輪播換到沒有曲繪的曲子時，只會把 Image 關掉、不清 sprite
        // —— 只比對 sprite 的話，空白的卡中間會浮著上一首的金框。
        if (cover.sprite != lastSprite || cover.enabled != lastEnabled ||
            box.rect.size != lastBox) Apply();
    }

    private void Apply()
    {
        if (cover == null || box == null || rect == null || edge == null) return;
        lastSprite = cover.sprite;
        lastBox = box.rect.size;
        lastEnabled = cover.enabled;

        if (lastSprite == null || !cover.enabled || lastBox.x < 1f || lastBox.y < 1f)
        {
            edge.enabled = false;
            return;
        }
        edge.enabled = true;

        float aspect = lastSprite.rect.width / Mathf.Max(0.0001f, lastSprite.rect.height);
        float boxAspect = lastBox.x / Mathf.Max(0.0001f, lastBox.y);
        float width = aspect >= boxAspect ? lastBox.x : lastBox.y * aspect;
        float height = aspect >= boxAspect ? lastBox.x / aspect : lastBox.y;
        rect.sizeDelta = new Vector2(width, height);
    }
}
