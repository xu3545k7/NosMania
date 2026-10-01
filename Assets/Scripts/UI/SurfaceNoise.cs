using UnityEngine;

/// <summary>
/// 會精確接邊的值噪聲：每一張程序貼圖的底層。
/// </summary>
/// <remarks>
/// **為什麼不用 `Mathf.PerlinNoise`。** 它在 u=0 和 u=1 取到的值不一樣，所以每鋪
/// 一次就有一條看得見的直縫。原本的解法是把同一個場的四份環繞副本交叉淡接
/// （`ClassicalBookUITheme.Tileable`）—— 縫是沒了，代價是**對比隨位置塌陷**：中央
/// 四份權重各 0.25，四個獨立場的平均把變異數壓成 1/4；角落只有一份，對比是滿的。
/// 實測角落 64×64 的標準差 0.155、中央同尺寸 0.0965，而鏽點的門檻是一個絕對值，
/// 所以 95% 的鏽點擠在晶格角落、中間一片空白。
///
/// 這裡改成把**格點索引對週期取模**：週期給多少，貼圖就在多少格之後接得回去，而
/// 且場的統計量在每一個位置都一樣。練習室的木牆（`PracticeRoomScreen`）本來就是
/// 這樣畫的 —— 這個檔案只是把它提出來共用。
///
/// **座標約定。** `Noise` / `Fbm` 吃的是**格座標**（一單位等於一格）；`Lattice` 吃
/// 的是**像素座標**加一個頻率，內部換算成 `round(size * frequency)` 格。舊的
/// `Tileable` 用的是後者，所以換過去的時候頻率數字可以照抄。
/// </remarks>
internal static class SurfaceNoise
{
    /// <summary>格點雜湊。`px`／`py` 是這個方向上一圈有幾格。</summary>
    public static float Hash(int x, int y, int px, int py)
    {
        return Hash(x, y, px, py, 0);
    }

    /// <summary>同上，另外給一個 seed 讓同一張貼圖裡的每一層互相獨立。</summary>
    public static float Hash(int x, int y, int px, int py, int seed)
    {
        if (px < 1) px = 1;
        if (py < 1) py = 1;
        int ax = ((x % px) + px) % px;
        int ay = ((y % py) + py) % py;
        // 一定要用 uint：C# 的 int 右移是**算術**位移（會延伸符號位），負數一進來
        // 高位就被灌成 1，雜湊的分佈整個歪掉。
        uint h = (uint)(ax * 374761393) + (uint)(ay * 668265263) + (uint)(seed * 1442695041);
        h ^= h >> 13;
        h *= 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFF) / 65535f;
    }

    /// <summary>雙線性＋smoothstep 的值噪聲。座標單位是格。</summary>
    public static float Noise(float x, float y, int px, int py)
    {
        return Noise(x, y, px, py, 0);
    }

    public static float Noise(float x, float y, int px, int py, int seed)
    {
        int xi = Mathf.FloorToInt(x);
        int yi = Mathf.FloorToInt(y);
        float fx = x - xi;
        float fy = y - yi;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash(xi, yi, px, py, seed);
        float b = Hash(xi + 1, yi, px, py, seed);
        float c = Hash(xi, yi + 1, px, py, seed);
        float d = Hash(xi + 1, yi + 1, px, py, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    /// <summary>u、v 走 0–1，px／py 是這個方向上第一個八度有幾格。</summary>
    public static float Fbm(float u, float v, int px, int py, int octaves)
    {
        float sum = 0f;
        float amp = 0.5f;
        float norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            int sx = px << i;
            int sy = py << i;
            sum += Noise(u * sx, v * sy, sx, sy) * amp;
            norm += amp;
            amp *= 0.5f;
        }
        return sum / Mathf.Max(0.0001f, norm);
    }

    // Worley / Voronoi 曾經在這裡，用來畫皮革的細胞脊。**不要加回來。**
    //
    // 它的種子是一格一顆的抖動方格，抖動不足時會收斂成正六角形蜂巢 —— 使用者一眼就
    // 退件了。而且這個缺陷很難用測的抓到：它在傅立葉上沒有尖峰（spike 3.5，織布是
    // 706），六重對稱諧波只有 0.045。要抓它得看徑向能量剖面（一格一顆種子的點過程是
    // hyperuniform，會把自己的低頻挖掉，所以剖面是帶通不是低通）和胞面積的變異係數
    // （0.457，離織布的 0.25 比離任何有機表面都近）。
    //
    // 皮現在在 `LeatherSurface`，摺痕是隨機高度場的分水線 —— 島大小不一、摺痕會分岔
    // 也會淡出，那才是 morocco 的拓撲。

    /// <summary>
    /// 像素座標版：`frequency` 沿用舊 `Tileable` 的數字，晶格週期取
    /// `round(size * frequency)`，所以在 `size` 像素之後精確接回去。
    /// </summary>
    public static float Lattice(float x, float y, float frequency, int seed, int size)
    {
        if (size < 1) size = 1;
        int period = Mathf.Max(1, Mathf.RoundToInt(size * frequency));
        float scale = period / (float)size;
        return Noise(x * scale, y * scale, period, period, seed);
    }
}
