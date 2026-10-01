using UnityEngine;

/// <summary>
/// 從譜面本身讀出速度與節奏特徵：BPM、每拍幾顆音、拍子佔用率。
/// </summary>
/// <remarks>
/// **這只服務 HUD 上的 BPM 數字。** 音符的流速不走這裡 —— 它看的是音符密度，和
/// 速度標記完全無關，見 <c>ChartScrollSpeed</c>。（第一版曾經想讓兩邊共用這一份，後來
/// 流速改成看密度，就只剩 BpmDisplay 一個客戶了。）
///
/// 還是放在單獨一個類別而不是散在 BpmDisplay 裡，是因為這條規則有好幾個不直覺
/// 的地方：基準要取開頭不是中位數、八度歧義要靠音符特徵分辨。
/// </remarks>
public static class ChartTempo
{
    /// <summary>正確的速度下每拍大約幾顆音符。兩首已知真值的實測值的幾何平均。</summary>
    public const float DensityTarget = 2.5f;

    /// <summary>取開頭幾格算基準。</summary>
    private const int OpeningSamples = 16;

    private static bool[] slotBuffer;

    /// <summary>
    /// 開頭幾格的中位間隔（毫秒）。
    /// </summary>
    /// <remarks>
    /// **不是整份的中位數。** <c>first_bpm</c> 的語意是「第一個速度」，而變速曲的整
    /// 份中位數混了後段——宣告值明明是對的卻會被判成錯。實測 712 份譜，用整份中位數
    /// 有 102 份不一致，改用開頭 16 格只剩 11 首。「初音ミクの消失」就是這樣被誤判
    /// 的：開頭 800 ms（= 75 BPM，和宣告值一致），整份中位數卻是 500 ms。
    /// </remarks>
    public static float OpeningIntervalMs(Chart chart)
    {
        if (chart == null || chart.beat_timings == null || chart.beat_timings.Count < 2)
            return 0f;
        int count = Mathf.Min(OpeningSamples, chart.beat_timings.Count - 1);
        var deltas = new System.Collections.Generic.List<int>(count);
        for (int i = 1; i <= count; i++)
        {
            int delta = chart.beat_timings[i] - chart.beat_timings[i - 1];
            if (delta > 0) deltas.Add(delta);
        }
        if (deltas.Count == 0) return 0f;
        deltas.Sort();
        return deltas[deltas.Count / 2];
    }

    /// <summary>兩個長度互為整數倍（±2%）嗎。</summary>
    /// <remarks>兩種慣例都有：有的譜一拍一條、有的一小節一條，所以兩個方向都要試。</remarks>
    public static bool IsIntegerRatio(float big, float small)
    {
        if (big <= 0f || small <= 0f) return false;
        float k = big / small;
        if (k < 0.98f) return false;
        float rounded = Mathf.Round(k);
        return rounded >= 1f && Mathf.Abs(k - rounded) <= 0.02f * Mathf.Max(1f, rounded);
    }

    /// <summary>
    /// 這個速度下，音符落在拍上的樣子。
    /// </summary>
    /// <param name="occupancy">有音符的拍佔幾成。速度猜太快就會掉下來。</param>
    /// <param name="density">每拍幾顆。速度猜太慢就會衝上去。</param>
    public static bool MeasureRhythm(Chart chart, float bpm, out float occupancy, out float density)
    {
        occupancy = 0f;
        density = 0f;
        if (chart == null || chart.notes == null || chart.notes.Count < 16 || bpm <= 0f)
            return false;

        float period = 60000f / bpm;
        int first = int.MaxValue;
        int last = int.MinValue;
        for (int i = 0; i < chart.notes.Count; i++)
        {
            int at = chart.notes[i].startTime;
            if (at < first) first = at;
            if (at > last) last = at;
        }
        if (last <= first) return false;

        int slots = Mathf.Max(1, Mathf.FloorToInt((last - first) / period) + 1);
        if (slotBuffer == null || slotBuffer.Length < slots)
            slotBuffer = new bool[Mathf.NextPowerOfTwo(slots)];
        System.Array.Clear(slotBuffer, 0, slots);

        int filled = 0;
        for (int i = 0; i < chart.notes.Count; i++)
        {
            int slot = Mathf.FloorToInt((chart.notes[i].startTime - first) / period);
            if (slot < 0 || slot >= slots) continue;
            if (!slotBuffer[slot])
            {
                slotBuffer[slot] = true;
                filled++;
            }
        }
        occupancy = filled / (float)slots;
        density = chart.notes.Count / (float)slots;
        return true;
    }

    /// <summary>
    /// 這份譜實際多快。宣告值對得上開頭的格線就用它，否則用音符特徵挑。
    /// </summary>
    /// <remarks>
    /// 候選都是格線的整數倍，光看間隔沒有任何資訊可以分辨八度——音符才有：速度取一
    /// 半時每拍會擠進兩倍的音，取一倍時一半的拍是空的。所以取佔用率過得了門檻、而每
    /// 拍顆數最接近 <see cref="DensityTarget"/> 的那一個。
    ///
    /// 這是估計不是真相。有些譜的格線本身就沒有對齊音樂上的拍，那種只能修資料。
    /// </remarks>
    public static float ResolveBpm(Chart chart, int beatsPerMeasure)
    {
        if (chart == null) return 0f;
        float declared = chart.first_bpm;
        float opening = OpeningIntervalMs(chart);
        if (opening <= 0f) return declared;

        if (declared > 0f)
        {
            float beatMs = 60000f / declared;
            if (IsIntegerRatio(beatMs, opening) || IsIntegerRatio(opening, beatMs))
                return declared;
        }

        float perLine = 60000f / opening;
        int numerator = Mathf.Max(1, beatsPerMeasure);
        // 0.5 也要試：有的譜一格是半拍。
        float[] guesses =
        {
            perLine * 0.5f, perLine, perLine * 2f, perLine * 3f,
            perLine * 4f, perLine * numerator, perLine * numerator * 2f,
        };

        float best = 0f;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < guesses.Length; i++)
        {
            float bpm = guesses[i];
            if (bpm < 45f || bpm > 340f) continue;
            float occupancy;
            float density;
            if (!MeasureRhythm(chart, bpm, out occupancy, out density)) continue;
            if (occupancy < 0.75f || density <= 0f) continue;
            float distance = Mathf.Abs(Mathf.Log(density / DensityTarget));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = bpm;
            }
        }
        if (best > 0f) return best;

        for (int i = 0; i < guesses.Length; i++)
        {
            if (guesses[i] >= 55f && guesses[i] <= 320f) return guesses[i];
        }
        return perLine;
    }
}
