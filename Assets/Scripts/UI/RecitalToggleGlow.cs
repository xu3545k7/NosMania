using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 演奏會開關的狀態：開／關、滑過、按下、鎖住。
/// </summary>
/// <remarks>
/// **為什麼不用 uGUI 的 ColorBlock。** `ColorBlock` 只有一個 `normalColor`，而這
/// 顆按鈕有**開**和**關**兩個靜止狀態；`Selectable` 的 `CrossFadeColor` 會直接覆
/// 寫 `targetGraphic.color`，開關的狀態色會被它吃掉。所以這顆的 `transition` 必須
/// 維持 `None`，狀態自己管。
///
/// 它左邊那顆分組鈕用 `ColorBlock` 是對的 —— 那顆只有一個狀態。兩者不一樣，不要
/// 為了「統一」把這顆也換過去。
///
/// 開關是**整個模式切換在畫面上的全部回饋**（按下去換的是背後那張房間照片，沒有
/// 對話框、沒有說明），所以它自己至少要有手感：滑過亮起來、按下去壓一格、鎖住的
/// 時候看得出來按不動。
/// </remarks>
internal sealed class RecitalToggleGlow : UIBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private static readonly Color FaceOff = new Color(0.70f, 0.64f, 0.56f, 1f);
    private static readonly Color LabelOff = new Color(0.62f, 0.55f, 0.45f, 1f);
    private static readonly Color LabelOn = new Color(1f, 0.88f, 0.62f, 1f);

    private Image face;
    private TextMeshProUGUI label;
    private RectTransform rect;

    private float glow;          // 滑過／按下
    private float glowTarget;
    private float on;            // 開／關（畫出來的值）
    private float onTarget;
    private float onFrom;
    private float onTime = -1f;
    private bool locked;
    private bool hovering;

    public void Bind(Image plate, TextMeshProUGUI text)
    {
        face = plate;
        label = text;
        rect = transform as RectTransform;
    }

    /// <summary>外面每次重畫開關時呼叫。immediate 用在第一次建立。</summary>
    public void SetState(bool recitalOn, bool isLocked, bool immediate)
    {
        locked = isLocked;
        float next = recitalOn ? 1f : 0f;
        if (immediate || !isActiveAndEnabled)
        {
            on = next;
            onTarget = next;
            onTime = -1f;
            glow = glowTarget = 0f;
            Apply();
            return;
        }
        if (!Mathf.Approximately(next, onTarget))
        {
            onTarget = next;
            onFrom = on;                 // 從**目前畫出來的值**接著走，不是從 0 或 1
            onTime = 0f;
        }
        if (locked) glowTarget = 0f;
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        if (!locked) glowTarget = 1f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        glowTarget = 0f;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!locked) glowTarget = 0.55f;
    }

    /// <summary>放開時要看游標還在不在牌面上。</summary>
    /// <remarks>
    /// uGUI 的 pointerUp 一律送回當初收到 down 的那個物件，所以「按住、拖出去、放
    /// 開」也會進到這裡 —— 無條件設回 1 的話，亮度和放大會留在按鈕上拔不掉，看起
    /// 來像游標還停在上面。
    /// </remarks>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (!locked) glowTarget = hovering ? 1f : 0f;
    }

    /// <summary>面板被收起來時把滑過狀態歸零。</summary>
    /// <remarks>
    /// 按下開關會在同一幀把整個選曲面板關掉，而物件被停用時 uGUI 不會補送
    /// OnPointerExit —— 下次回到選曲畫面，開關會停在「亮著而且放大」的樣子。
    /// </remarks>
    protected override void OnDisable()
    {
        base.OnDisable();
        hovering = false;
        glow = glowTarget = 0f;
        Apply();
    }

    private void Update()
    {
        bool dirty = false;
        if (!Mathf.Approximately(glow, glowTarget))
        {
            glow = Mathf.MoveTowards(glow, glowTarget, Time.unscaledDeltaTime / 0.18f);
            dirty = true;
        }
        if (onTime >= 0f)
        {
            onTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(onTime / 0.16f);
            on = Mathf.Lerp(onFrom, onTarget, 1f - Mathf.Pow(1f - t, 3f));
            if (t >= 1f)
            {
                on = onTarget;
                onTime = -1f;
            }
            dirty = true;
        }
        if (dirty) Apply();
    }

    private void Apply()
    {
        float dim = locked ? 0.85f : 1f;
        if (face != null)
        {
            float lift = Mathf.Lerp(1f, 1.18f, glow) * dim;
            Color tone = Color.Lerp(FaceOff, Color.white, on);
            face.color = new Color(Mathf.Clamp01(tone.r * lift), Mathf.Clamp01(tone.g * lift),
                Mathf.Clamp01(tone.b * lift), 1f);
        }
        if (label != null)
        {
            Color text = Color.Lerp(LabelOff, LabelOn, on);
            label.color = new Color(text.r * dim, text.g * dim, text.b * dim, 1f);
        }
        // 放大只有 1.8%：按鈕是貼著分類條排的，再多就會看起來沒對齊。
        if (rect != null) rect.localScale = Vector3.one * Mathf.Lerp(1f, 1.018f, glow);
    }
}
