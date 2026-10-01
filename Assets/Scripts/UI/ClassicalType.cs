using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全專案共用的字級表。同一階在任何畫面上都是同一個實際大小。
/// </summary>
/// <remarks>
/// **為什麼需要這張表。** 這個專案的畫布不只一種參考解析度：練習室和幾乎所有覆蓋層
/// 是 1920×1080，選曲畫面是 2560×1440。兩者都是 <c>ScaleWithScreenSize</c>，所以
/// 「20」在前者是一個大小、在後者小掉四分之一 —— 同一套設計語言照抄數字過去，字就
/// 會莫名其妙變細。
///
/// **為什麼是問畫布而不是乘一個常數。** 乘 4/3 得到的結果一樣，但那個 4/3 是我算出
/// 來、寫死在某個檔案裡的：哪天有人改了任何一邊的參考解析度，那個數字就默默變成錯
/// 的，而症狀只是「字看起來怪怪的」。<see cref="On"/> 是去問那個畫布自己的
/// <c>referenceResolution</c>，所以改了就自動跟著對。
///
/// **階數取自練習室。** 那一套（14／16／20／24／32／40）是在 1080 高的畫布上調出來
/// 的，也是目前唯一被完整用過一輪的一套，所以基準就定在那裡。
/// </remarks>
public static class ClassicalType
{
    /// <summary>這張表是在多高的畫布上訂的。</summary>
    private const float BaseHeight = 1080f;

    /// <summary>卡片小標、註解。</summary>
    public const float Caption = 14f;
    /// <summary>內文、次要按鈕。</summary>
    public const float Body = 16f;
    /// <summary>按鈕、副標。</summary>
    public const float Label = 20f;
    /// <summary>清單曲名、區塊標題。</summary>
    public const float Title = 24f;
    /// <summary>數字、畫面標題。</summary>
    public const float Display = 32f;
    /// <summary>主數值、大曲名。</summary>
    public const float Hero = 40f;

    /// <summary>抬頭的字距。只給抬頭用 —— 大數字加了字距會散成一排互不相干的數字。</summary>
    public const float Tracking = 3f;

    /// <summary>
    /// 這一階在 <paramref name="anywhere"/> 所在的那個畫布上該是多少。
    /// </summary>
    /// <remarks>
    /// 找不到畫布或它不是 <c>ScaleWithScreenSize</c> 時直接回基準值 —— 那通常表示
    /// 這是個固定像素的畫布，換算反而會錯。
    /// </remarks>
    public static float On(Component anywhere, float step)
    {
        if (anywhere == null) return step;
        Canvas canvas = anywhere.GetComponentInParent<Canvas>();
        if (canvas == null) return step;
        var scaler = canvas.rootCanvas != null
            ? canvas.rootCanvas.GetComponent<CanvasScaler>()
            : canvas.GetComponent<CanvasScaler>();
        if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            return step;
        float height = scaler.referenceResolution.y;
        return height > 1f ? step * height / BaseHeight : step;
    }
}
