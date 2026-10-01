using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// The words the game uses to describe a chart, and the levels at which each one
/// applies.
/// </summary>
/// <remarks>
/// One place on purpose.  The bookmark panel and the score-book overlay both
/// describe the same analysis, and when each kept its own copy of the list they
/// drifted apart: renaming the techniques in one left the other showing the old
/// words and missing the ones added since.
///
/// Every threshold is measured rather than guessed: the 80th percentile of the
/// library, or the 90th for the measures most charts score zero on. A word then
/// means "unusually much of this" and not merely "has some", and a tag that
/// fires for the median chart tells the player nothing.
///
/// The numbers below were re-measured over the library's 336 registered charts,
/// 326 of them carrying technique data. This has now drifted twice, both times
/// the same way: the library grows, the new charts are mostly easier, and every
/// threshold that was a percentile of the old library is left sitting too high.
/// The second pass found hand speed at what had become the 95th percentile, so
/// "fast" had quietly stopped being sayable -- and the radar's speed spoke was
/// drawn below the median ring for a chart in the library's top fifth.
///
/// **Re-measure in this order**, because the aggregate axes normalise by the
/// member thresholds: member thresholds first, then the radar anchors from the
/// aggregates those new thresholds produce.
/// </remarks>
public static class ChartAnalysisVocabulary
{
    // ---- 門檻 ----------------------------------------------------------------
    // 全部量自現行曲庫的**登錄譜**：照每首 register.json 的 chartFileName 解析，
    // 336 份，其中 326 份帶技法資料。（不要用資料夾 glob 去數 —— 兩個曲庫幾乎是彼此
    // 的複本，共有的譜會被算兩次，而且有些譜不在難度子資料夾裡，掃不到。）
    // 常見項取第 80 百分位；多數譜為 0 的稀有項取第 90，否則「有一點」就會被當成
    // 「特別多」。這些是 const 而不是散落的字面值：同一個數字在描述、技法選字和
    // 雷達圖三個地方都要用，各寫一份就會各自漂走。
    //
    // 跨度／靈巧的錨點原本是在 336 份上量的（那時 register 的路徑解析還漏了 26 份），
    // 已補量到和速度／耐力同一個母體 362 份。
    //
    // 2026-09-30 重量（上一次是曲庫只有 123 張的時候）。曲庫長到快三倍，多出來的
    // 多半是中低難度，所以中位和第 80 百分位整體往下走了 —— 最極端的是單手擊鍵速
    // 度 32.2 → 18、長押 0.298 → 0.168。**頂端幾乎沒動**（雷達的「稀有」錨點還是
    // 對得上第 97 百分位），變的是「一般的譜長什麼樣」。

    public const float BurstThreshold = 2.905f;
    /// <summary>耐力負荷的第 80 百分位。單位是「超出休息水位的音符數」，不是秒。</summary>
    public const float StaminaThreshold = 7179f;
    /// <summary>加權節奏點密度的第 80 百分位。雷達「速度」軸用這一個。</summary>
    public const float SpeedLoadThreshold = 34f;
    /// <summary>未加權的節奏點密度的第 80 百分位。留給描述用。</summary>
    public const float OnsetRateThreshold = 21f;
    /// <summary>單手擊鍵速度；只給「單手極速」這個標籤用。</summary>
    public const float HandSpeedThreshold = 19f;
    public const float ChordThreshold = 2.074f;
    /// <summary>Peak notes per second in one hand: chords taken at speed.</summary>
    public const float ChordRateThreshold = 27f;
    public const float HoldThreshold = 0.168f;
    public const float HandBiasThreshold = 0.096f;
    /// <summary>72% 的譜完全沒有手勢音符，所以用第 90 百分位。</summary>
    public const float SlideThreshold = 0.0214f;
    public const float IndependenceThreshold = 0.0699f;

    public const float LeapThreshold = 0.355f;
    /// <summary>
    /// Fast leaps as a share of every move, the same denominator as leaps.
    /// </summary>
    /// <remarks>
    /// This used to be measured against "moves taken that fast", which made it a
    /// conditional probability rather than an amount, and let a sparse chart
    /// with a handful of quick jumps outrank one that leaps at speed throughout.
    /// The measure changed with cache version 5, and this threshold with it:
    /// 0.151 under the old denominator, 0.080 under this one.
    /// </remarks>
    public const float FastLeapThreshold = 0.060f;
    public const float OctaveThreshold = 0.164f;
    public const float OctaveRunThreshold = 0.048f;
    public const float ArpeggioThreshold = 0.114f;
    public const float FingerRunThreshold = 0.118f;
    public const float FastRunThreshold = 0.028f;
    /// <summary>Doubled onsets inside fast passagework, as a share of all notes.</summary>
    public const float ThickRunThreshold = 0.0113f;
    public const float TrillThreshold = 0.019f;
    /// <summary>88% 為 0，取第 90 百分位。</summary>
    public const float RepeatThreshold = 0.0037f;
    /// <summary>79% 為 0，取第 90 百分位。</summary>
    public const float HandCrossThreshold = 0.0066f;

    /// <summary>One measure, with the level at which it stops being ordinary.</summary>
    public readonly struct Trait
    {
        public readonly string name;
        public readonly float value;
        public readonly float threshold;

        public Trait(string name, float value, float threshold)
        {
            this.name = name;
            this.value = value;
            this.threshold = threshold;
        }

        public bool Notable => value >= threshold;
        /// <summary>How far past ordinary, so the most distinctive traits rank first.</summary>
        public float Score => threshold > 0.0001f ? value / threshold : 0f;
    }

    /// <summary>
    /// The techniques the hands are asked for, as ratios that can be shown as
    /// percentages.
    /// </summary>
    public static void CollectTechniques(ChartAnalysis analysis, List<Trait> into)
    {
        if (analysis == null || into == null) return;

        // Finger independence comes from note lengths, so it is available even on
        // the charts that carry no pitch at all.
        into.Add(new Trait(Localize.T("手指獨立", "手指独立", "Independence"),
            analysis.independenceRatio, IndependenceThreshold));

        if (!analysis.hasTechniqueData) return;

        // Speed changes what a technique is.  A chart that takes its octaves and
        // its jumps at tempo is described by the fast name instead of the plain
        // one, so the two never spend two slots saying one thing.
        bool fastOctaves = analysis.octaveRunRatio >= OctaveRunThreshold;
        bool fastLeaps = analysis.fastLeapRatio >= FastLeapThreshold;
        bool fastRuns = analysis.fastRunRatio >= FastRunThreshold;

        into.Add(fastLeaps
            ? new Trait(Localize.T("高速跳躍", "高速跳跃", "Fast leaps"), analysis.fastLeapRatio, FastLeapThreshold)
            : new Trait(Localize.T("大跨度跳躍", "大跨度跳跃", "Leaps"), analysis.leapRatio, LeapThreshold));
        into.Add(fastOctaves
            ? new Trait(Localize.T("高速八度", "高速八度", "Fast octaves"), analysis.octaveRunRatio, OctaveRunThreshold)
            : new Trait(Localize.T("八度", "八度", "Octaves"), analysis.octaveRatio, OctaveThreshold));
        into.Add(new Trait(Localize.T("琶音", "琶音", "Arpeggios"), analysis.arpeggioRatio, ArpeggioThreshold));
        into.Add(fastRuns
            ? new Trait(Localize.T("高速運指", "高速运指", "Fast runs"), analysis.fastRunRatio, FastRunThreshold)
            : new Trait(Localize.T("音階跑動", "音阶跑动", "Scale runs"), analysis.fingerRunRatio, FingerRunThreshold));
        into.Add(new Trait(Localize.T("雙音跑動", "双音跑动", "Double-stop runs"),
            analysis.thickRunRatio, ThickRunThreshold));
        into.Add(new Trait(Localize.T("顫音", "颤音", "Trills"), analysis.trillRatio, TrillThreshold));
        into.Add(new Trait(Localize.T("同音反覆", "同音反复", "Repeated notes"), analysis.repeatRatio, RepeatThreshold));
        into.Add(new Trait(Localize.T("交叉手", "交叉手", "Hand crossing"), analysis.handCrossRatio, HandCrossThreshold));
    }

    /// <summary>
    /// One radar spoke: the value, and the three levels of the library it is
    /// read against.
    /// </summary>
    /// <remarks>
    /// A single threshold is not a common standard. The six measures have very
    /// different distributions -- measured over the 210 charts that carry
    /// technique data, a hold ratio of 0.40 is the 95th percentile while a peak
    /// hand rate of 33 is the 80th, and the composite reach and agility axes
    /// take a maximum of two and three ratios, so their "1.0" lands nearer the
    /// 65th. Scaled by one number each, the same distance from the centre meant
    /// something different on every spoke.
    ///
    /// Each axis instead carries the library's median, 80th and 95th
    /// percentile, and is drawn through them onto the same fixed radii. A ring
    /// then means one thing everywhere: the bright ring is "busier than four
    /// charts in five", on every spoke and on every song.
    /// </remarks>
    public readonly struct RadarAxis
    {
        /// <summary>Where the library's median sits, as a fraction of the spoke.</summary>
        public const float MedianRadius = 0.40f;
        /// <summary>Where the 80th percentile sits -- the ring worth naming.</summary>
        public const float NotableRadius = 0.70f;

        public readonly string name;
        public readonly float value;
        public readonly float median;
        public readonly float notable;
        public readonly float rare;

        public RadarAxis(string name, float value, float median, float notable, float rare)
        {
            this.name = name;
            this.value = value;
            this.median = median;
            this.notable = notable;
            this.rare = rare;
        }

        public bool Notable => value >= notable;

        /// <summary>How far out to plot, 0 at the centre and 1 at the outer ring.</summary>
        public float Fraction
        {
            get
            {
                if (value <= 0f) return 0f;
                if (value < median)
                    return MedianRadius * value / Mathf.Max(median, 0.0001f);
                if (value < notable)
                    return Mathf.Lerp(MedianRadius, NotableRadius,
                        (value - median) / Mathf.Max(notable - median, 0.0001f));
                if (value < rare)
                    return Mathf.Lerp(NotableRadius, 1f,
                        (value - notable) / Mathf.Max(rare - notable, 0.0001f));
                return 1f;
            }
        }
    }

    /// <summary>
    /// The six axes of the summary radar, each against the library's own spread.
    /// </summary>
    /// <remarks>
    /// Percentiles measured over the library, not estimated. Reach and agility
    /// are composites, so their anchors were re-measured after the thresholds
    /// they normalise by moved -- an aggregate shifts whenever any member does.
    ///
    /// Reach gathers leaps, octaves and hand crossing; agility gathers scale
    /// runs, arpeggios, trills and repeated notes. A spoke apiece would ask the
    /// same question several times over -- leaps and octaves are both "how far
    /// must the hand travel" -- and dilute each other into a polygon that always
    /// sits near the centre.
    ///
    /// Chord weight is measured from 1.0, not from 0: every chart has at least
    /// one note per onset, so counting from zero would give a chart with no
    /// chords at all a spoke nearly half way out.
    /// </remarks>
    public static void CollectRadar(ChartAnalysis analysis, List<RadarAxis> into)
    {
        if (analysis == null || into == null) return;

        // 前四軸的分位點量自全部 336 份登錄譜，後兩軸只有帶技法資料的 326 份有值，
        // 所以分位點也量自那 326 份 —— 拿沒有資料的譜一起算會把中位數往下拉。
        // **節奏點密度，不是音符密度，也不是單手。** 快慢是「拍點來得多密」：
        // 一個厚和弦是一個拍點不是五個（拿音符密度當速度，弦多就被讀成快，
        // 而且和旁邊的和弦軸講同一件事）；一串在兩手之間分工的音階，和全給右手
        // 讀起來一樣快。**但密度低也不代表不快** —— 空隙被大跳吃掉的話，給你
        // 跳的時間就很少，所以每一個發聲點要再乘上「跳得多急」。
        // 見 <see cref="ChartAnalysis.speedLoad"/>。
        into.Add(new RadarAxis(Localize.T("速度", "速度", "Speed"),
            analysis.speedLoad, 16.0f, SpeedLoadThreshold, 64.0f));
        // 量的是「超出可以無限撐下去的水位的音符數，連續越久權重越高」：高速單點
        // 吃運指、連續砸和弦吃手臂，兩種都累，所以強度算音符不是拍點（和速度軸
        // 相反）；而一段連續 120 秒比分散的四段 30 秒更貴。見 <see cref="ChartAnalysis.staminaLoad"/>。
        into.Add(new RadarAxis(Localize.T("耐力", "耐力", "Stamina"),
            analysis.staminaLoad, 591f, StaminaThreshold, 19798f));
        // 這一軸畫的是 ChordLoad 而不是每個發聲點幾顆音：厚度本身和難度無關，
        // 快慢才有關，而純粹的快已經是速度軸了。見 ChartAnalysis.ChordLoad。
        into.Add(new RadarAxis(Localize.T("和弦", "和弦", "Chords"),
            analysis.ChordLoad, 2f, 6f, 17f));
        into.Add(new RadarAxis(Localize.T("長押", "长按", "Holds"),
            analysis.HoldRatio, 0.040f, HoldThreshold, 0.435f));
        // 這一軸只收**快的**位移。一個慢慢移過去的大跳沒有難度可言，而
        // leapRatio / octaveRatio 是不分快慢的，所以那兩個留給標籤列去描述，
        // 雷達圖用它們的高速版本。
        into.Add(new RadarAxis(Localize.T("跨度", "跨度", "Reach"),
            Aggregate(Ratio(analysis.fastLeapRatio, FastLeapThreshold),
                      Ratio(analysis.octaveRunRatio, OctaveRunThreshold),
                      Ratio(analysis.handCrossRatio, HandCrossThreshold)),
            0.654f, 1.413f, 4.136f));
        into.Add(new RadarAxis(Localize.T("靈巧", "灵巧", "Agility"),
            Aggregate(Ratio(analysis.fingerRunRatio, FingerRunThreshold),
                      Ratio(analysis.arpeggioRatio, ArpeggioThreshold),
                      Ratio(analysis.trillRatio, TrillThreshold),
                      Ratio(analysis.repeatRatio, RepeatThreshold),
                      Ratio(analysis.thickRunRatio, ThickRunThreshold)),
            0.420f, 1.884f, 7.271f));
    }

    private static float Ratio(float value, float threshold)
    {
        return threshold > 0.0001f ? value / threshold : 0f;
    }

    /// <summary>
    /// Combines the members of one composite axis: close to the strongest, but
    /// a chart that does several of them gets credit for all of them.
    /// </summary>
    /// <remarks>
    /// A plain maximum threw away everything but the winner, so hand crossing --
    /// which 70% of charts never do at all -- could only ever show up on the
    /// radar by out-scoring leaps and octaves outright, and otherwise vanished.
    /// A sum is the opposite mistake: four ordinary techniques would then read
    /// as one extraordinary one.
    ///
    /// The cube norm sits between the two. One member at 1.5 gives 1.5; three
    /// members at 1.0 give 1.44, so breadth counts for something without
    /// drowning out depth.
    /// </remarks>
    private static float Aggregate(params float[] members)
    {
        float total = 0f;
        for (int i = 0; i < members.Length; i++)
        {
            float value = Mathf.Max(0f, members[i]);
            total += value * value * value;
        }

        return total > 0f ? Mathf.Pow(total, 1f / 3f) : 0f;
    }

    /// <summary>
    /// The strongest techniques with their numbers, whether or not they clear the
    /// threshold: "no leaps at all" is information too.
    /// </summary>
    public static string DescribeTechniques(ChartAnalysis analysis, int maximum = 3)
    {
        if (analysis == null) return string.Empty;

        var traits = new List<Trait>(9);
        CollectTechniques(analysis, traits);
        traits.Sort((a, b) => b.Score.CompareTo(a.Score));

        var text = new StringBuilder(96);
        int shown = 0;
        for (int i = 0; i < traits.Count && shown < maximum; i++)
        {
            if (traits[i].value < 0.005f) continue;
            if (shown > 0) text.Append("   ");
            text.Append(traits[i].name).Append(' ')
                .Append((traits[i].value * 100f).ToString("0")).Append('%');
            shown++;
        }
        return shown > 0 ? text.ToString() : Localize.T("無明顯技法", "无明显技法", "None");
    }

    /// <summary>
    /// A short read on what makes the chart hard, from the shape of its own
    /// numbers rather than from its authored level.  At most three, ranked by how
    /// far past ordinary each one is, so what comes out is what stands out.
    /// </summary>
    public static string DescribeCharacter(ChartAnalysis analysis)
    {
        if (analysis == null) return string.Empty;

        var traits = new List<Trait>(16)
        {
            new Trait(Localize.T("爆發", "爆发", "Burst"), analysis.Burstiness, BurstThreshold),
            // Endurance is time spent busy, not average business: a short sprint
            // and a six minute grind can share an average and feel nothing alike.
            new Trait(Localize.T("耐力", "耐力", "Stamina"), analysis.staminaLoad, StaminaThreshold),
            // Strikes per second in one hand, chords counted once, so this stays
            // a speed measure instead of restating chord weight.
            new Trait(Localize.T("單手極速", "单手极速", "Hand speed"), analysis.peakHandRate, HandSpeedThreshold),
            // 厚不等於難：慢速的六聲部聖詠在「每個發聲點幾顆音」上是滿分，彈起來
            // 卻不難。和弦一旦是快的，說的就是另一件事，所以換一個詞和一個量。
            analysis.peakChordRate >= ChordRateThreshold
                ? new Trait(Localize.T("高速和弦", "高速和弦", "Fast chords"),
                    analysis.peakChordRate, ChordRateThreshold)
                : new Trait(Localize.T("和弦厚重", "和弦厚重", "Chord-heavy"),
                    analysis.NotesPerOnset, ChordThreshold),
            new Trait(Localize.T("長押多", "长按多", "Hold-heavy"), analysis.HoldRatio, HoldThreshold),
            new Trait(Localize.T("偏一手", "偏一手", "One-sided"), analysis.HandBias, HandBiasThreshold),
            // Gesture notes are authored, not derived, and 89% of the library
            // has none at all, so the bar is the 90th percentile rather than
            // the 80th -- otherwise a single gesture would win the tag line.
            new Trait(Localize.T("滑音", "滑音", "Slides"), analysis.TechnicalRatio, SlideThreshold),
        };
        CollectTechniques(analysis, traits);
        traits.Sort((a, b) => b.Score.CompareTo(a.Score));

        var tags = new List<string>(3);
        for (int i = 0; i < traits.Count && tags.Count < 3; i++)
            if (traits[i].Notable) tags.Add(traits[i].name);

        if (tags.Count == 0) return Localize.T("平穩", "平稳", "Even");
        return string.Join("  ·  ", tags);
    }
}
