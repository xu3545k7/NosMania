using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>How the song list is split into the cards of the top bar.</summary>
public enum SongGroupingMode
{
    /// <summary>The collection each song was filed under.</summary>
    Genre = 0,
    /// <summary>One card per difficulty level, hardest first.</summary>
    Level = 1,
}

/// <summary>
/// The bar above the song carousel: a mode selector plus a sliding strip of
/// category cards.  The strip uses the same <see cref="CarouselScroller"/> as
/// the songs, so it spins continuously and snaps to whichever card is nearest.
/// Changing the collection reloads the whole song list, so the change is only
/// committed once the strip has settled, never mid-slide.
/// </summary>
public sealed class SongGroupingBar
{
    /// <summary>Raised once the strip has come to rest on a different card.</summary>
    public event Action<int> CategoryCommitted;
    /// <summary>Raised when the player picks a different grouping from the dropdown.</summary>
    public event Action<SongGroupingMode> ModeChanged;

    private const int CardCount = 5;      // roles -2 .. +2
    private const int MaxRole = 2;
    private const float SpacingRatio = 0.60f;   // of the viewport width
    private const float CardWidthRatio = 0.56f;

    private sealed class Card
    {
        public RectTransform root;
        public Graphic plate;
        public TextMeshProUGUI label;
        public CanvasGroup group;
        public int role;
    }

    private readonly CarouselScroller scroller = new CarouselScroller();
    private readonly List<Card> cards = new List<Card>();
    private readonly List<string> entries = new List<string>();
    private readonly List<Button> modeOptionButtons = new List<Button>();
    private readonly List<TextMeshProUGUI> modeOptionLabels = new List<TextMeshProUGUI>();
    private readonly List<CheckMarkGraphic> modeOptionTicks = new List<CheckMarkGraphic>();
    private readonly List<SongGroupingMode> optionModes = new List<SongGroupingMode>();

    private RectTransform frame;
    private RectTransform viewport;
    private RectTransform songPositionRoot;
    private TextMeshProUGUI songPositionLabel;
    private RectTransform modeButtonRoot;
    private TextMeshProUGUI modeButtonLabel;
    private RectTransform popupRoot;

    private bool dragCandidate;
    private bool dragMoved;
    private bool pointerWasPressed;
    private float dragStartPointerX;
    private float clickSuppressedUntil;

    public SongGroupingMode Mode { get; private set; } = SongGroupingMode.Genre;
    public bool IsBuilt => frame != null;
    public bool PointerOverBar { get; private set; }
    public bool IsPopupOpen => popupRoot != null && popupRoot.gameObject.activeSelf;
    /// <summary>True while the strip is still moving, so the caller can hold off on expensive work.</summary>
    public bool IsSliding => scroller.Moving;
    /// <summary>The card currently nearest the middle, even mid-slide.</summary>
    public int FocusedIndex => scroller.BaseIndex;

    // ---- construction -----------------------------------------------------

    /// <summary>
    /// Upgrades the existing category frame in place: the old single label is
    /// retired in favour of a clipped, sliding strip, and a mode dropdown is
    /// added to its left.
    /// </summary>
    public void Build(RectTransform existingFrame, TextMeshProUGUI retiredLabel, SongGroupingMode initialMode)
    {
        if (existingFrame == null || IsBuilt) return;
        frame = existingFrame;
        Mode = initialMode;

        if (retiredLabel != null) retiredLabel.gameObject.SetActive(false);

        viewport = CreateChild(frame, "CategoryViewport");
        viewport.anchorMin = new Vector2(0.14f, 0f);
        viewport.anchorMax = new Vector2(0.86f, 1f);
        viewport.offsetMin = new Vector2(6f, 4f);
        viewport.offsetMax = new Vector2(-6f, -4f);
        // Neighbouring cards must not spill out over the gold frame.
        viewport.gameObject.AddComponent<RectMask2D>();

        for (int i = 0; i < CardCount; i++)
        {
            int role = i - MaxRole;
            RectTransform cardRoot = CreateChild(viewport, $"CategoryCard_{role}");
            cardRoot.anchorMin = new Vector2(0.5f, 0f);
            cardRoot.anchorMax = new Vector2(0.5f, 1f);
            cardRoot.pivot = new Vector2(0.5f, 0.5f);

            // 分類卡是書籤，不是按鈕。這一列回答的是「現在翻到書的哪一段」，
            // 而這整個畫面是書、燙金和緞帶 —— 矩形是這裡唯一沒有的形狀。
            // CanvasRenderer 明寫出來。分類卡原本掛的是 Image，它自己會帶一個 —— 換
            // 成自訂的 Graphic 之後就沒了，而缺少它只有在 RectMask2D 要裁切的那一刻
            // 才會炸，看起來和繪製完全無關。
            if (cardRoot.GetComponent<CanvasRenderer>() == null)
                cardRoot.gameObject.AddComponent<CanvasRenderer>();
            var plate = cardRoot.gameObject.AddComponent<ClassicalTabGraphic>();
            plate.color = ClassicalBookUITheme.Wood;
            plate.raycastTarget = false;

            RectTransform labelRoot = CreateChild(cardRoot, "Label");
            labelRoot.offsetMin = new Vector2(14f, 5f);
            labelRoot.offsetMax = new Vector2(-14f, -5f);
            var label = labelRoot.gameObject.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 15f;
            label.fontSizeMax = 27f;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            label.color = ClassicalBookUITheme.ParchmentLight;
            label.raycastTarget = false;
            ClassicalBookUITheme.ApplyLocalizedFont(label);

            cards.Add(new Card
            {
                root = cardRoot,
                plate = plate,
                label = label,
                group = cardRoot.gameObject.AddComponent<CanvasGroup>(),
                role = role,
            });
        }

        BuildSongPositionLabel();
        BuildModeSelector();
        UpdateModeButtonCaption();
    }

    private void BuildSongPositionLabel()
    {
        songPositionRoot = CreateChild(frame, "SongPosition");
        songPositionRoot.anchorMin = new Vector2(0f, 0f);
        songPositionRoot.anchorMax = new Vector2(1f, 0f);
        songPositionRoot.pivot = new Vector2(0.5f, 1f);
        songPositionRoot.anchoredPosition = new Vector2(0f, -6f);
        songPositionRoot.sizeDelta = new Vector2(0f, 22f);

        songPositionLabel = songPositionRoot.gameObject.AddComponent<TextMeshProUGUI>();
        songPositionLabel.alignment = TextAlignmentOptions.Center;
        songPositionLabel.fontSize = 14f;
        songPositionLabel.fontStyle = FontStyles.Bold;
        songPositionLabel.color = ClassicalBookUITheme.AgedPaper;
        songPositionLabel.raycastTarget = false;
        ClassicalBookUITheme.ApplyLocalizedFont(songPositionLabel);
    }

    private void BuildModeSelector()
    {
        RectTransform parent = frame.parent as RectTransform;
        if (parent == null) return;

        modeButtonRoot = CreateChild(parent, "GroupingModeButton");
        // 分類槽是固定 1180 寬的一條，分組鈕貼在它左邊。分數錨點在寬螢幕上會把這顆
        // 推到槽外面去，所以用固定值。
        //
        // −684 不是 −614：分類的「‹」已經從槽裡搬到槽外，佔掉 −669..−604，而這顆鈕
        // 從 −614 起算就正好壓在它上面 —— 實機上左箭頭整顆看不見。往左讓 70，留 15
        // 的縫。（原本的 −614 是為了和右邊的演奏會開關對稱，而那顆開關已經退場。）
        modeButtonRoot.anchorMin = new Vector2(0.5f, 1f);
        modeButtonRoot.anchorMax = new Vector2(0.5f, 1f);
        modeButtonRoot.pivot = new Vector2(1f, 1f);
        // 改成一顆漏斗：「音樂類型」這四個字只是在說「這是一個篩選器」，
        // 而漏斗圖示說的是同一件事、只要四分之一的寬度。目前選了哪一個
        // 在下拉選單裡用勾勾標記，不必写在鈕上。
        // 跟著槽的寬度走：槽半寬 320，左箭頭占 −365..−325，漏斗緊貼在它外面。
        modeButtonRoot.anchoredPosition = new Vector2(-375f, frame.anchoredPosition.y);
        modeButtonRoot.sizeDelta = new Vector2(52f, frame.sizeDelta.y);

        var buttonImage = modeButtonRoot.gameObject.AddComponent<Image>();
        ClassicalBookUITheme.StylePlate(buttonImage, false);

        RectTransform iconRoot = CreateChild(modeButtonRoot, "Funnel");
        iconRoot.anchorMin = new Vector2(0.5f, 0.5f);
        iconRoot.anchorMax = new Vector2(0.5f, 0.5f);
        iconRoot.pivot = new Vector2(0.5f, 0.5f);
        iconRoot.anchoredPosition = Vector2.zero;
        iconRoot.sizeDelta = new Vector2(22f, 24f);
        var funnel = iconRoot.gameObject.AddComponent<FunnelIconGraphic>();
        funnel.color = ClassicalBookUITheme.ParchmentLight;
        funnel.raycastTarget = false;

        // 標籤還在，只是不上場：<c>UpdateModeButtonCaption</c> 和本地化都會寫它，
        // 拿掉的話那兩條路都要跘空指標。
        RectTransform captionRoot = CreateChild(modeButtonRoot, "Label");
        captionRoot.anchorMin = Vector2.zero;
        captionRoot.anchorMax = Vector2.one;
        captionRoot.offsetMin = Vector2.zero;
        captionRoot.offsetMax = Vector2.zero;
        modeButtonLabel = captionRoot.gameObject.AddComponent<TextMeshProUGUI>();
        modeButtonLabel.raycastTarget = false;
        captionRoot.gameObject.SetActive(false);

        var button = modeButtonRoot.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        ColorBlock buttonColors = button.colors;
        buttonColors.normalColor = Color.white;
        buttonColors.highlightedColor = new Color(1.28f, 1.12f, 0.76f, 1f);
        buttonColors.pressedColor = new Color(0.72f, 0.62f, 0.48f, 1f);
        buttonColors.fadeDuration = 0.10f;
        button.colors = buttonColors;
        button.onClick.AddListener(TogglePopup);

        BuildPopup(parent);
    }

    private void BuildPopup(RectTransform parent)
    {
        popupRoot = CreateChild(parent, "GroupingModePopup");
        popupRoot.anchorMin = modeButtonRoot.anchorMin;
        popupRoot.anchorMax = modeButtonRoot.anchorMax;
        // 和鈕同一個樞紐。(0.5,1) 配上鈕的 (1,1)，選單會橫移半個寬。
        popupRoot.pivot = modeButtonRoot.pivot;
        popupRoot.anchoredPosition = modeButtonRoot.anchoredPosition +
            new Vector2(0f, -frame.sizeDelta.y - 6f);

        var background = popupRoot.gameObject.AddComponent<Image>();
        ClassicalBookUITheme.ApplyDarkSurface(background,
            new Color(0.13f, 0.06f, 0.03f, 0.99f), ClassicalBookUITheme.Surface.Paper, 4f);
        var outline = popupRoot.gameObject.AddComponent<Outline>();
        outline.effectColor = ClassicalBookUITheme.Gold;
        outline.effectDistance = new Vector2(2f, -2f);
        var shadow = popupRoot.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(6f, -6f);

        var layout = popupRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var modes = (SongGroupingMode[])Enum.GetValues(typeof(SongGroupingMode));
        for (int i = 0; i < modes.Length; i++)
        {
            SongGroupingMode mode = modes[i];
            RectTransform optionRoot = CreateChild(popupRoot, $"Option_{mode}");

            var optionImage = optionRoot.gameObject.AddComponent<Image>();
            ClassicalBookUITheme.StylePlate(optionImage, true);
            var optionLayout = optionRoot.gameObject.AddComponent<LayoutElement>();
            optionLayout.preferredHeight = 38f;

            RectTransform optionLabelRoot = CreateChild(optionRoot, "Label");
            optionLabelRoot.anchorMin = Vector2.zero;
            optionLabelRoot.anchorMax = Vector2.one;
            // 右邊留 26 給勾勾：標籤居中而勾勾靠右，兩個搶位置的話長名字會壓上去。
            optionLabelRoot.offsetMin = new Vector2(10f, 0f);
            optionLabelRoot.offsetMax = new Vector2(-26f, 0f);
            var optionLabel = optionLabelRoot.gameObject.AddComponent<TextMeshProUGUI>();
            optionLabel.text = DescribeMode(mode);
            optionLabel.alignment = TextAlignmentOptions.Center;
            optionLabel.fontSize = 15f;
            optionLabel.fontStyle = FontStyles.Bold;
            optionLabel.color = ClassicalBookUITheme.ParchmentLight;
            optionLabel.raycastTarget = false;
            ClassicalBookUITheme.ApplyLocalizedFont(optionLabel);

            var optionButton = optionRoot.gameObject.AddComponent<Button>();
            optionButton.targetGraphic = optionImage;
            ColorBlock optionColors = optionButton.colors;
            optionColors.normalColor = Color.white;
            optionColors.highlightedColor = new Color(1.34f, 1.16f, 0.76f, 1f);
            optionColors.pressedColor = new Color(0.70f, 0.58f, 0.42f, 1f);
            optionColors.fadeDuration = 0.08f;
            optionButton.colors = optionColors;
            SongGroupingMode captured = mode;
            optionButton.onClick.AddListener(() => SelectMode(captured));
            RectTransform tickRoot = CreateChild(optionRoot, "Tick");
            tickRoot.anchorMin = new Vector2(1f, 0.5f);
            tickRoot.anchorMax = new Vector2(1f, 0.5f);
            tickRoot.pivot = new Vector2(1f, 0.5f);
            tickRoot.anchoredPosition = new Vector2(-8f, 0f);
            tickRoot.sizeDelta = new Vector2(16f, 16f);
            var tick = tickRoot.gameObject.AddComponent<CheckMarkGraphic>();
            tick.color = ClassicalBookUITheme.Gold;
            tick.raycastTarget = false;

            modeOptionButtons.Add(optionButton);
            modeOptionLabels.Add(optionLabel);
            modeOptionTicks.Add(tick);
            optionModes.Add(mode);
        }

        // **寬度不能留 0。**
        //
        // popupRoot 複製了鈕的錯點 (0.5,1)/(0.5,1) —— 零尺寸錯點，所以 sizeDelta
        // 就是實際尺寸，而這裡只給了高。寬 0 的 VerticalLayoutGroup 把每一個選項
        // 壓成 0 寬，標籤就一個字一行往下排 —— 實機上看到的是一條直的字。
        // 這行應該是在鈕還是拉伸錯點的年代寫的，鈕改成固定寬之後就沒人跟著改。
        popupRoot.sizeDelta =
            new Vector2(modeButtonRoot.sizeDelta.x, modes.Length * 42f + 12f);
        popupRoot.gameObject.SetActive(false);
        RefreshOptionHighlight();
    }

    /// <summary>Marks which grouping is currently in force.</summary>
    private void RefreshOptionHighlight()
    {
        for (int i = 0; i < modeOptionButtons.Count && i < optionModes.Count; i++)
        {
            var graphic = modeOptionButtons[i].targetGraphic as Image;
            if (graphic == null) continue;
            bool current = optionModes[i] == Mode;
            graphic.color = current
                ? new Color(0.46f, 0.22f, 0.09f, 1f)
                : new Color(0.30f, 0.115f, 0.085f, 1f);
            // 勾勾是該選項是不是目前這一個的唯一記號 —— 底色的差別在深色選單
            // 上太微弱，靠它一個人讀不出來。
            if (i < modeOptionTicks.Count && modeOptionTicks[i] != null)
                modeOptionTicks[i].gameObject.SetActive(current);
        }
    }

    private static RectTransform CreateChild(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static string DescribeMode(SongGroupingMode mode)
    {
        switch (mode)
        {
            case SongGroupingMode.Level:
                return Localize.T("等級", "等级", "Level");
            default:
                return Localize.T("音樂類型", "音乐类型", "Genre");
        }
    }

    // ---- content ----------------------------------------------------------

    /// <summary>Replace the card captions and place the strip on one of them.</summary>
    public void SetEntries(IReadOnlyList<string> names, int focusedIndex)
    {
        entries.Clear();
        if (names != null)
        {
            for (int i = 0; i < names.Count; i++) entries.Add(names[i] ?? string.Empty);
        }

        scroller.SetCount(entries.Count);
        scroller.SnapTo(focusedIndex);
        RefreshCardText();
        Layout();
    }

    public void SetSongPosition(int songNumber, int songCount)
    {
        if (songPositionLabel == null) return;
        songPositionLabel.text = songCount > 0 ? $"{songNumber} / {songCount}" : string.Empty;
    }

    public void RefreshLocalization()
    {
        UpdateModeButtonCaption();
        for (int i = 0; i < modeOptionLabels.Count && i < optionModes.Count; i++)
        {
            TextMeshProUGUI label = modeOptionLabels[i];
            if (label == null) continue;
            label.text = DescribeMode(optionModes[i]);
            ClassicalBookUITheme.ApplyLocalizedFont(label);
        }
    }

    private void UpdateModeButtonCaption()
    {
        if (modeButtonLabel == null) return;
        // The previous down-triangle is absent from several CJK fallback fonts
        // and appeared as a square. The framed button already communicates that
        // this caption is selectable, so no unsupported glyph is needed.
        modeButtonLabel.text = DescribeMode(Mode);
        // The font is picked from the text, so it has to be re-resolved whenever
        // the caption changes language.
        ClassicalBookUITheme.ApplyLocalizedFont(modeButtonLabel);
    }

    private void RefreshCardText()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            Card card = cards[i];
            if (card.label == null) continue;
            card.label.text = entries.Count > 0
                ? entries[scroller.WrapIndex(scroller.BaseIndex + card.role)]
                : string.Empty;
            // Category captions come from the song library and must retain their
            // original spelling even when the interface language changes.
            ClassicalBookUITheme.ApplyContentFont(card.label);
        }
    }

    // ---- input and motion -------------------------------------------------

    /// <summary>Step the strip by one card; used by the frame's arrow buttons.</summary>
    public void Step(int direction)
    {
        if (IsClickSuppressed()) return;
        scroller.Nudge(direction >= 0 ? 1 : -1);
    }

    /// <summary>
    /// Drives the strip for one frame.  The caller owns pointer and wheel
    /// reading so the song carousel and this bar cannot both act on one notch.
    /// </summary>
    public void Tick(float deltaTime, float wheelDelta, bool pointerPressed, Vector2 pointerScreenPosition,
        Camera uiCamera)
    {
        if (!IsBuilt) return;

        PointerOverBar = RectTransformUtility.RectangleContainsScreenPoint(
            frame, pointerScreenPosition, uiCamera);

        if (PointerOverBar && Mathf.Abs(wheelDelta) > 0.01f)
        {
            int steps = scroller.ConsumeWheel(wheelDelta);
            // Wheel up walks towards the previous card.
            if (steps != 0) scroller.Nudge(-steps);
        }

        HandleDrag(deltaTime, pointerPressed, pointerScreenPosition, uiCamera);
        HandlePopupDismiss(pointerPressed, pointerScreenPosition, uiCamera);

        scroller.Tick(deltaTime, out bool baseChanged, out bool settled);
        if (baseChanged) RefreshCardText();
        Layout();
        if (settled) CategoryCommitted?.Invoke(scroller.BaseIndex);
        pointerWasPressed = pointerPressed;
    }

    private void HandleDrag(float deltaTime, bool pointerPressed, Vector2 pointerScreenPosition, Camera uiCamera)
    {
        if (entries.Count <= 1)
        {
            dragCandidate = false;
            dragMoved = false;
            return;
        }

        if (!dragCandidate)
        {
            // A press that began elsewhere must not grab the strip on its way past.
            if (!pointerPressed || pointerWasPressed || !PointerOverBar || IsPopupOpen) return;
            dragCandidate = true;
            dragMoved = false;
            dragStartPointerX = pointerScreenPosition.x;
            return;
        }

        if (pointerPressed)
        {
            float pixels = pointerScreenPosition.x - dragStartPointerX;
            // Below the threshold this is still a click on an arrow button.
            if (!dragMoved && Mathf.Abs(pixels) < 12f) return;
            if (!dragMoved)
            {
                dragMoved = true;
                scroller.BeginDrag();
            }
            scroller.DragTo(scroller.DragAnchorScroll - pixels / GetPixelsPerCard(uiCamera), deltaTime);
            return;
        }

        dragCandidate = false;
        if (dragMoved)
        {
            scroller.EndDrag();
            // Swallow the click the drag would otherwise deliver on release.
            clickSuppressedUntil = Time.unscaledTime + 0.15f;
        }
        dragMoved = false;
    }

    private void HandlePopupDismiss(bool pointerPressed, Vector2 pointerScreenPosition, Camera uiCamera)
    {
        if (!IsPopupOpen || !pointerPressed) return;
        bool onPopup = RectTransformUtility.RectangleContainsScreenPoint(
            popupRoot, pointerScreenPosition, uiCamera);
        bool onButton = modeButtonRoot != null && RectTransformUtility.RectangleContainsScreenPoint(
            modeButtonRoot, pointerScreenPosition, uiCamera);
        if (!onPopup && !onButton) SetPopupOpen(false);
    }

    private float GetPixelsPerCard(Camera uiCamera)
    {
        float viewportWidth = viewport != null ? viewport.rect.width : 200f;
        float scale = frame != null ? frame.lossyScale.x : 1f;
        return Mathf.Max(1f, viewportWidth * SpacingRatio * Mathf.Max(0.01f, scale));
    }

    private bool IsClickSuppressed()
    {
        return dragMoved || Time.unscaledTime < clickSuppressedUntil;
    }

    private void Layout()
    {
        if (viewport == null) return;
        Rect rect = viewport.rect;
        float spacing = rect.width * SpacingRatio;
        Vector2 cardSize = new Vector2(rect.width * CardWidthRatio, rect.height);
        float frac = scroller.Frac;

        for (int i = 0; i < cards.Count; i++)
        {
            Card card = cards[i];
            if (card.root == null) continue;

            float distance = card.role - frac;
            float absDistance = Mathf.Abs(distance);
            float weight = Mathf.Clamp01(absDistance);
            float eased = weight * weight * (3f - 2f * weight);

            // The neighbours stay legible but clearly secondary; anything beyond
            // one card out is only there to cover the edge while sliding.
            float alpha = Mathf.Clamp01(1f - absDistance * 0.72f);
            bool visible = entries.Count > 0 && alpha > 0.01f;
            if (card.root.gameObject.activeSelf != visible) card.root.gameObject.SetActive(visible);
            if (!visible) continue;

            card.root.sizeDelta = cardSize;
            card.root.anchoredPosition = new Vector2(spacing * distance, 0f);
            card.root.localScale = Vector3.one * Mathf.Lerp(1f, 0.82f, eased);
            if (card.group != null) card.group.alpha = alpha;
            // 焦點的強調改由牌面的**染色**負責：中間那張是完整的牌，兩側調暗調淡。
            // 原本是把金色描邊加粗加亮，但描邊只是把整張圖再畫一次偏移的副本 ——
            // 加粗只會讓邊緣更糊。而且這裡的染色是乘在牌面貼圖上的，原本那組給
            // 平塗用的暗色（0.10～0.30）會把整張牌乘成近黑。
            // 焦點的強調由緞帶本身的顏色負責：中間那條是完整的酒紅，兩側退回
            // 暗木色並且淡出。緞帶的絲光和金線是從這個顏色推出來的，所以整條
            // 帶子會一起亮起來，而不是只有邊框變色。
            if (card.plate != null)
                card.plate.color = Color.Lerp(
                    new Color(ClassicalBookUITheme.Wood.r, ClassicalBookUITheme.Wood.g,
                        ClassicalBookUITheme.Wood.b, 0.68f),
                    ClassicalBookUITheme.Burgundy, 1f - eased);
        }
    }

    // ---- mode dropdown ----------------------------------------------------

    private void TogglePopup()
    {
        if (IsClickSuppressed()) return;
        SetPopupOpen(!IsPopupOpen);
    }

    public void SetPopupOpen(bool open)
    {
        if (popupRoot == null) return;
        popupRoot.gameObject.SetActive(open);
        if (open) popupRoot.SetAsLastSibling();
    }

    private void SelectMode(SongGroupingMode mode)
    {
        SetPopupOpen(false);
        if (mode == Mode) return;
        Mode = mode;
        UpdateModeButtonCaption();
        RefreshOptionHighlight();
        ModeChanged?.Invoke(mode);
    }
}
