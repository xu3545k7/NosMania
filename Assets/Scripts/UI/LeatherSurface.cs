using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 書皮的粒面：一張烤出來的 256×256 純量場，摺痕是分水嶺的分水線。
/// </summary>
/// <remarks>
/// **為什麼不是 Worley。** 上一版的皮是兩層 Worley F1，而 Worley 的種子是一格一顆的
/// 抖動方格 —— 抖動不足時它會收斂成正六角形蜂巢，使用者一眼就看出來了（「不要都六
/// 角形」）。量測上這件事很難抓：它在傅立葉上**沒有尖峰**（spike 3.5，織布是 706），
/// 六重對稱諧波 a3 只有 0.045。抓得到它的是兩個指標 ——
///   1. 徑向能量剖面「從 r=4 爬到 r=16 才開始掉」。一格一顆種子的點過程是
///      hyperuniform，它會把自己的低頻挖掉，所以是帶通不是低通。全專案其他每一個表面
///      都是單調下降的。
///   2. 胞面積的變異係數 0.457 —— 離織布的 0.25 比離任何有機表面都近（本專案最好的
///      有機面 cover 是 4.45）。「每一格都一樣大」就是那句抱怨的內容。
///
/// **為什麼是分水嶺。** 真皮的粒面（boarded morocco）是**大小不等的鼓起島嶼**，島之
/// 間的摺痕會分岔、會淡出、而且不是每一條都閉合。那正是一個隨機高度場的分水線網路的
/// 拓撲 —— 不是一個把平面切乾淨的鑲嵌。高度場用整數波向量的餘弦和（所以精確接邊），
/// 分水嶺跑兩次：原尺度給次級粒（7.2px ≈ 1.33mm，對得上好山羊皮「每吋 20 條」的
/// fine break），模糊過的複本給 boarding 的主網（22.8px ≈ 4.2mm）。
///
/// **最關鍵的一件事：它是起伏，不是遮罩。** 前四版都把摺痕當成一張暗色遮罩畫在平面
/// 上，每一版的數字都好看，每一版看起來都像乾掉的泥巴或長頸鹿。改成「建一個 relief
/// 再打光」（定向 emboss ＋ 摺痕裡的遮蔽）之後才讀得出是皮 —— 人眼判斷皮革靠的就是
/// 島鼓起來的那一道高光。那一步只花 8 個 op。
///
/// 烤出來的光固定從左上（-0.62, -0.78），和專案其他地方的受光方向同一邊。
///
/// **已知的結構性風險。** 場是 172 條離散餘弦的和，所以它的傅立葉變換裡真的有 172 個
/// delta。靠的是振幅上限讓任何一條都不到可見的程度（最大單一模態 0.186 sd，對照舊皮
/// 0.104、紙的簾紋 0.354）。**換 seed 要重新用眼睛驗一次** —— 某一條可能落到顯眼的
/// 地方。結構性的解法是加更多模態，成本是線性的（光是 grain 那一段就佔 794 ops 裡的
/// 392）。
///
/// 成本：整張烤完 14.1 ms（單執行緒，實測），而它取代的那支是 55.5 ms。
/// </remarks>
internal static class LeatherSurface
{
    public const int Size = 256;

    /// <summary>烤完之後的均值與標準差（實測，不是目標值）。</summary>
    public const float Mean = 0.810196f;

    private const float TargetMean = 0.810f;
    private const float TargetSd = 0.045f;

    private static float[] cached;

    /// <summary>
    /// 256×256 的純量場，0–1。第一次叫會烤 14 ms，之後是同一個陣列。
    /// </summary>
    /// <remarks>
    /// 快取的是**場**不是 Sprite。`ApplyDarkSurface` 的每一個 gain 都會產生一張自己的
    /// 貼圖，而 gain 只是事後的線性變換 —— 場跟 gain 無關。快取錯層的話，第二個 gain
    /// 會把整個分水嶺重解一次。
    /// </remarks>
    public static float[] Field()
    {
        if (cached != null) return cached;
        cached = Bake();
        return cached;
    }

    // ------------------------------------------------------------------
    // 波表：整數波向量的隨機相位高斯場
    // ------------------------------------------------------------------

    private struct Wave
    {
        public int nx, ny;
        public float amp, ph;
    }

    private static uint IHash(int i, int salt)
    {
        unchecked
        {
            uint h = (uint)(i * 374761393 + salt * 668265263 + 1442695041);
            h ^= h >> 13;
            h *= 1274126177u;
            h ^= h >> 16;
            return h;
        }
    }

    private static double Rnd(int i, int salt) => IHash(i, salt) / 4294967296.0;

    /// <summary>
    /// 挑一組整數波向量。`nx`／`ny` 是整數，所以每一條餘弦在 256 之後精確接回去 ——
    /// 接邊是**構造上**成立的，不是量出來剛好。
    /// </summary>
    /// <remarks>
    /// 兩段徑向分佈：K1 條在對數軸上均分（低頻要夠密，否則某一條低頻正弦會獨自扛下整
    /// 個低頻帶，畫出來是 45° 的燈芯絨 —— 第二版就是這樣），K2 條在 kmid–kmax 之間按
    /// 面積均分（那才是粒面所在的那一圈）。
    ///
    /// 角度用黃金比例序列鋪開再加抖動，避免任何一個方向被偏好。
    ///
    /// 最後那個 6 次的迭代是**振幅上限**：沒有任何一條可以超過整場標準差的 capLo–capHi
    /// 倍（高頻嚴、低頻鬆）。這是「不會看到單一條紋」的唯一保證。
    /// </remarks>
    private static Wave[] WaveTable(int K1, int K2, double kmin, double kmid, double kmax,
                                    double beta, double rollK, double rollW, int seed,
                                    double capLo, double capHi, double capK0, double capK1)
    {
        var list = new List<Wave>();
        var caps = new List<double>();
        var used = new HashSet<long>();
        double lnr = Math.Log(kmax / kmin);
        double disc = kmax * kmax - kmid * kmid;
        for (int j = 0; j < K1 + K2; j++)
        {
            double r;
            if (j < K1) { double t = (j + 0.5) / K1; r = kmin * Math.Exp(t * lnr); }
            else { double t = (j - K1 + 0.5) / K2; r = Math.Sqrt(kmid * kmid + t * disc); }
            r *= 0.90 + 0.20 * Rnd(j, seed + 9);
            double th = Math.PI * ((j * 0.6180339887498949 + 0.31 * Rnd(j, seed + 5)) % 1.0);
            int nx = (int)Math.Round(r * Math.Cos(th));
            int ny = (int)Math.Round(r * Math.Sin(th));
            bool ok = false;
            for (int t2 = 0; t2 < 30; t2++)
            {
                // 半平面正規化：(nx,ny) 和 (-nx,-ny) 是同一條餘弦，收成同一份才不會
                // 有兩條同頻的波互相拍打。
                if (ny < 0 || (ny == 0 && nx < 0)) { nx = -nx; ny = -ny; }
                if ((nx != 0 || ny != 0) && !used.Contains(((long)nx << 20) ^ ny))
                { ok = true; break; }
                if (t2 % 2 == 0) nx++; else ny++;
            }
            if (!ok) continue;
            used.Add(((long)nx << 20) ^ ny);
            double rr = Math.Sqrt((double)nx * nx + (double)ny * ny);
            double dens = K1 / (rr * lnr);
            if (rr >= kmid && rr <= kmax) dens += K2 * 2.0 * rr / disc;
            if (dens < 1e-6) dens = 1e-6;
            double roll = 1.0 / (1.0 + Math.Pow(rr / rollK, rollW));
            double power = Math.Pow(rr, -beta) * roll;
            double amp = Math.Sqrt(2.0 * power / dens) * (0.80 + 0.40 * Rnd(j, seed + 17));
            double ph = 2.0 * Math.PI * Rnd(j, seed + 23);
            double u = Math.Min(1.0, Math.Max(0.0, (rr - capK0) / (capK1 - capK0)));
            list.Add(new Wave { nx = nx, ny = ny, amp = (float)amp, ph = (float)ph });
            caps.Add(capLo + (capHi - capLo) * (u * u * (3.0 - 2.0 * u)));
        }
        for (int pass = 0; pass < 6; pass++)
        {
            double s2 = 0.0;
            for (int i = 0; i < list.Count; i++) s2 += (double)list[i].amp * list[i].amp;
            double sig = Math.Sqrt(s2 * 0.5);
            bool hit = false;
            for (int i = 0; i < list.Count; i++)
            {
                double lim = caps[i] * sig;
                if (list[i].amp > lim * 1.0001)
                {
                    Wave w = list[i];
                    w.amp = (float)lim;
                    list[i] = w;
                    hit = true;
                }
            }
            if (!hit) break;
        }
        return list.ToArray();
    }

    /// <summary>
    /// 把波表求和成一張場。
    /// </summary>
    /// <remarks>
    /// 用 `cos(kx·x + ky·y + p) = cos(kx·x)cos(ky·y+p) − sin(kx·x)sin(ky·y+p)` 拆開，
    /// 所以 x 方向的三角函數每條波只算一次（n 次），內層迴圈只剩兩個乘加。直接算
    /// `Cos(...)` 的話是 n² 次三角函數，慢一個量級。
    /// </remarks>
    private static void SpectralField(Wave[] waves, int n, float[] dst)
    {
        Array.Clear(dst, 0, dst.Length);
        var cx = new float[n];
        var sx = new float[n];
        double tau = 2.0 * Math.PI / n;
        foreach (Wave w in waves)
        {
            for (int x = 0; x < n; x++)
            {
                double a = tau * w.nx * x;
                cx[x] = (float)Math.Cos(a);
                sx[x] = (float)Math.Sin(a);
            }
            for (int y = 0; y < n; y++)
            {
                double b = tau * w.ny * y + w.ph;
                float cb = (float)(w.amp * Math.Cos(b));
                float sb = (float)(w.amp * Math.Sin(b));
                int row = y * n;
                for (int x = 0; x < n; x++) dst[row + x] += cb * cx[x] - sb * sx[x];
            }
        }
    }

    /// <summary>就地正規化成均值 0、標準差 1。</summary>
    private static void Unit(float[] a)
    {
        double m = 0.0;
        for (int i = 0; i < a.Length; i++) m += a[i];
        m /= a.Length;
        double v = 0.0;
        for (int i = 0; i < a.Length; i++) { double d = a[i] - m; v += d * d; }
        float s = (float)(1.0 / Math.Max(1e-9, Math.Sqrt(v / a.Length)));
        for (int i = 0; i < a.Length; i++) a[i] = (float)((a[i] - m) * s);
    }

    /// <summary>環繞的 Catmull-Rom 放大。大尺度的起伏在 128 烤，省一半波的成本。</summary>
    private static void UpsampleCR(float[] src, int ns, float[] dst, int nd)
    {
        int f = nd / ns;
        var mid = new float[ns * nd];
        for (int y = 0; y < ns; y++)
            for (int i = 0; i < ns; i++)
                for (int k = 0; k < f; k++)
                {
                    double t = k / (double)f;
                    double w0 = -0.5 * t + t * t - 0.5 * t * t * t;
                    double w1 = 1.0 - 2.5 * t * t + 1.5 * t * t * t;
                    double w2 = 0.5 * t + 2.0 * t * t - 1.5 * t * t * t;
                    double w3 = -0.5 * t * t + 0.5 * t * t * t;
                    float a = src[y * ns + ((i - 1 + ns) % ns)];
                    float b = src[y * ns + i];
                    float c = src[y * ns + ((i + 1) % ns)];
                    float d = src[y * ns + ((i + 2) % ns)];
                    mid[y * nd + i * f + k] = (float)(w0 * a + w1 * b + w2 * c + w3 * d);
                }
        for (int x = 0; x < nd; x++)
            for (int i = 0; i < ns; i++)
                for (int k = 0; k < f; k++)
                {
                    double t = k / (double)f;
                    double w0 = -0.5 * t + t * t - 0.5 * t * t * t;
                    double w1 = 1.0 - 2.5 * t * t + 1.5 * t * t * t;
                    double w2 = 0.5 * t + 2.0 * t * t - 1.5 * t * t * t;
                    double w3 = -0.5 * t * t + 0.5 * t * t * t;
                    float a = mid[((i - 1 + ns) % ns) * nd + x];
                    float b = mid[i * nd + x];
                    float c = mid[((i + 1) % ns) * nd + x];
                    float d = mid[((i + 2) % ns) * nd + x];
                    dst[(i * f + k) * nd + x] = (float)(w0 * a + w1 * b + w2 * c + w3 * d);
                }
    }

    // ------------------------------------------------------------------
    // 分水嶺
    // ------------------------------------------------------------------

    private static readonly int[] DY8 = { -1, -1, -1, 0, 0, 1, 1, 1 };
    private static readonly int[] DX8 = { -1, 0, 1, -1, 1, -1, 0, 1 };
    private static readonly int[] DY4 = { -1, 1, 0, 0 };
    private static readonly int[] DX4 = { 0, 0, -1, 1 };

    /// <summary>
    /// 每個像素指向八鄰裡最陡的上坡，然後反覆跟著指標跳，直到每個像素都指到自己盆地
    /// 的頂點。14 次跳躍足夠涵蓋這個尺度下最長的坡。
    /// </summary>
    /// <remarks>
    /// 斜對角要乘 1/√2，否則盆地會沿著對角線拉長 —— 那是會看得出來的方向性。
    /// </remarks>
    private static void Watershed(float[] h, int[] lab, float[] peak, int[] scratch)
    {
        const int n = Size;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                float best = 0f;
                int parent = i;
                for (int k = 0; k < 8; k++)
                {
                    int yy = (y + DY8[k] + n) % n;
                    int xx = (x + DX8[k] + n) % n;
                    int j = yy * n + xx;
                    float inv = (DY8[k] != 0 && DX8[k] != 0) ? 0.70710678f : 1f;
                    float g = (h[j] - h[i]) * inv;
                    if (g > best) { best = g; parent = j; }
                }
                lab[i] = parent;
            }
        int[] a = lab;
        int[] b = scratch;
        for (int it = 0; it < 14; it++)
        {
            for (int i = 0; i < n * n; i++) b[i] = a[a[i]];
            int[] t = a; a = b; b = t;
        }
        if (!ReferenceEquals(a, lab)) Array.Copy(a, lab, n * n);
        for (int i = 0; i < n * n; i++) peak[i] = h[lab[i]];
    }

    /// <summary>
    /// 分水線的「動態」：站在兩個盆地交界上，要淹到多高才會合流。
    /// </summary>
    /// <remarks>
    /// 用動態而不是「是不是邊界」，摺痕才會**有深淺**：兩個小島之間是淺痕，兩片大區之
    /// 間是深溝。這就是主網和次網從同一個機制長出來的原因。
    /// </remarks>
    private static void DivideField(float[] h, int[] lab, float[] peak, float[] dst)
    {
        const int n = Size;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                float w = 0f;
                for (int k = 0; k < 4; k++)
                {
                    int yy = (y + DY4[k] + n) % n;
                    int xx = (x + DX4[k] + n) % n;
                    int j = yy * n + xx;
                    if (lab[j] == lab[i] || h[i] > h[j]) continue;
                    float d = Math.Min(peak[i], peak[j]) - h[i];
                    if (d > w) w = d;
                }
                dst[i] = w;
            }
    }

    private static void Blur121(float[] a, int passes, float[] tmp)
    {
        const int n = Size;
        for (int p = 0; p < passes; p++)
        {
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    tmp[y * n + x] = 0.25f * a[y * n + ((x + n - 1) % n)]
                                   + 0.5f * a[y * n + x]
                                   + 0.25f * a[y * n + ((x + 1) % n)];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    a[y * n + x] = 0.25f * tmp[((y + n - 1) % n) * n + x]
                                 + 0.5f * tmp[y * n + x]
                                 + 0.25f * tmp[((y + 1) % n) * n + x];
        }
    }

    /// <summary>環繞的方框模糊，用滑動和，所以和半徑無關。</summary>
    private static void BoxBlur(float[] a, int r, int passes, float[] tmp)
    {
        const int n = Size;
        int w = 2 * r + 1;
        float inv = 1f / w;
        for (int p = 0; p < passes; p++)
        {
            for (int y = 0; y < n; y++)
            {
                float acc = 0f;
                for (int k = -r; k <= r; k++) acc += a[y * n + ((k + n) % n)];
                for (int x = 0; x < n; x++)
                {
                    tmp[y * n + x] = acc * inv;
                    acc += a[y * n + ((x + r + 1) % n)] - a[y * n + ((x - r + n) % n)];
                }
            }
            for (int x = 0; x < n; x++)
            {
                float acc = 0f;
                for (int k = -r; k <= r; k++) acc += tmp[((k + n) % n) * n + x];
                for (int y = 0; y < n; y++)
                {
                    a[y * n + x] = acc * inv;
                    acc += tmp[((y + r + 1) % n) * n + x] - tmp[((y - r + n) % n) * n + x];
                }
            }
        }
    }

    private static float SmoothStep(float t)
    {
        if (t < 0f) t = 0f;
        else if (t > 1f) t = 1f;
        return t * t * (3f - 2f * t);
    }

    private static void WhiteNoise(int seed, float[] dst)
    {
        const int n = Size;
        unchecked
        {
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                    h ^= h >> 13;
                    h *= 1274126177u;
                    h ^= h >> 16;
                    dst[y * n + x] = (h & 0xFFFFFF) / 16777216f;
                }
        }
    }

    // ------------------------------------------------------------------

    private static float[] Bake()
    {
        const int n = Size;
        const int count = n * n;

        // grain = 粒面那一圈（7.2px ≈ 1.33mm），swell = 整張皮的起伏（放鬆、磨損、色調
        // 都掛在它身上）。swell 在 128 烤再放大，因為它本來就沒有細節。
        Wave[] grain = WaveTable(24, 74, 5.0, 13.0, 54.0, 2.10, 28.0, 3.0, 1785,
                                 0.34, 0.11, 6.0, 14.0);
        Wave[] swell = WaveTable(22, 52, 1.6, 4.5, 18.0, 2.60, 14.0, 2.6, 5366,
                                 0.42, 0.20, 3.0, 10.0);

        var g = new float[count];
        SpectralField(grain, n, g);
        Unit(g);

        var low = new float[128 * 128];
        SpectralField(swell, 128, low);
        var s = new float[count];
        UpsampleCR(low, 128, s, n);
        Unit(s);

        // 粒的粗細跟著大起伏走：鬆的地方粒粗，繃緊的地方粒細。真皮就是這樣。
        var h = new float[count];
        for (int i = 0; i < count; i++)
        {
            float gmod = 1f + 0.62f * (2f * SmoothStep(s[i] * 0.55f + 0.5f) - 1f);
            h[i] = g[i] * gmod + 0.34f * s[i];
        }
        Unit(h);

        var lab = new int[count];
        var scratch = new int[count];
        var peak = new float[count];
        var dyn = new float[count];
        var tmp = new float[count];

        // 次級摺痕：原尺度的分水線。
        //
        // 0.12 的門檻是看出來的，不是算出來的：原本是 0.45，畫出來不是網路而是一堆斷
        // 開的箭頭 —— 只有最深的鞍點活下來。再加 0.16 的底，讓每一條分水線至少淡淡地
        // 存在，網路才連得起來。
        Watershed(h, lab, peak, scratch);
        DivideField(h, lab, peak, dyn);
        var crease = new float[count];
        for (int i = 0; i < count; i++)
        {
            float c = SmoothStep((dyn[i] - 0.12f) / (1.70f - 0.12f));
            crease[i] = dyn[i] > 0f ? 0.16f + 0.84f * c : 0f;
        }
        Blur121(crease, 1, tmp);
        for (int i = 0; i < count; i++) crease[i] = Math.Min(1f, crease[i] * 2f);

        // 主網（boarding 輾出來的那一層，22.8px ≈ 4.2mm）：同一個場模糊過再跑一次分水
        // 嶺。同一個機制生出兩級階層，這正是參考資料描述 morocco 的樣子。
        //
        // blur 4 / gain 2.2 是最後一輪用眼睛調的：blur 3 / gain 3.0 的線太細太硬，鋪成
        // 4×4 之後讀成龜裂（crazing）不是皮。
        var hc = (float[])h.Clone();
        BoxBlur(hc, 3, 2, tmp);
        Watershed(hc, lab, peak, scratch);
        DivideField(hc, lab, peak, dyn);
        var prim = new float[count];
        for (int i = 0; i < count; i++)
        {
            float c = SmoothStep((dyn[i] - 0.20f) / (1.30f - 0.20f));
            prim[i] = dyn[i] > 0f ? 0.10f + 0.90f * c : 0f;
        }
        Blur121(prim, 4, tmp);
        for (int i = 0; i < count; i++) prim[i] = Math.Min(1f, prim[i] * 2.2f);

        var micro = new float[count];
        WhiteNoise(907, micro);
        Blur121(micro, 1, tmp);
        var pore = new float[count];
        WhiteNoise(3313, pore);
        Blur121(pore, 1, tmp);

        // 起伏本身。摺痕是**刻進去**的（減），不是畫上去的。
        // 磨損的地方（wear）摺痕被壓平一半 —— 翻久的書角就是這樣。
        var relief = new float[count];
        var creaseW = new float[count];
        for (int i = 0; i < count; i++)
        {
            float wear = SmoothStep(s[i] * 0.75f + 0.5f);
            creaseW[i] = (crease[i] + 1.15f * prim[i]) * (1f - 0.50f * wear);
            float m = micro[i] - 0.5f;
            pore[i] = SmoothStep((pore[i] - 0.615f) / 0.115f);
            relief[i] = h[i] * 0.22f - creaseW[i] * 1.00f + m * 0.20f * (1f - 0.5f * wear);
        }

        // 打光。這是整支程式最重要的八行 —— 把起伏的梯度對上一個固定的光向，島就鼓起
        // 來了。前四版都是把摺痕當遮罩畫在平面上，每一版的數字都好看，每一版看起來都
        // 像乾掉的泥巴。
        var value = new float[count];
        var emboss = new float[count];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                float gx = relief[y * n + ((x + 1) % n)] - relief[y * n + ((x + n - 1) % n)];
                float gy = relief[((y + 1) % n) * n + x] - relief[((y + n - 1) % n) * n + x];
                emboss[i] = -(gx * -0.62f + gy * -0.78f);
            }
        Unit(emboss);

        // 遮蔽：摺痕深處本來就照不到光，那是和 emboss 不同的一件事。
        var ao = new float[count];
        for (int i = 0; i < count; i++) ao[i] = -creaseW[i];
        Unit(ao);
        var po = new float[count];
        for (int i = 0; i < count; i++) po[i] = -pore[i];
        Unit(po);
        for (int i = 0; i < count; i++)
            value[i] = emboss[i] * 1.00f + ao[i] * 0.95f + s[i] * 0.70f + po[i] * 0.26f;
        Unit(value);

        // 軟膝：把極端值壓回來。放大 2.5 倍之後再被 Clamp01 切掉的話，摺痕最深處會變
        // 成一塊死黑的平面 —— 那比對比不足還難看。
        for (int i = 0; i < count; i++)
        {
            float d = value[i] * TargetSd;
            float ad = Math.Abs(d);
            const float knee = 0.105f, ceiling = 0.175f;
            if (ad > knee)
            {
                float e = (ad - knee) / (ceiling - knee);
                ad = knee + (ceiling - knee) * e / (1f + e);
                d = d < 0f ? -ad : ad;
            }
            value[i] = TargetMean + d;
        }
        return value;
    }
}
