using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>一門技階課：三首譜，外加它的應試結果。</summary>
[Serializable]
public sealed class SyllabusCourse
{
    public int stage;
    /// <summary>三份譜的 chartFileName。順序就是應試順序（由易到難）。</summary>
    public List<string> charts = new List<string>();
    /// <summary>等級帶往外放寬了幾級才湊滿。0 = 沒放寬。</summary>
    public int widened;
    /// <summary>抽出來的三首在這一階的指標上都趨近於零 —— 曲庫裡沒有這個技法。</summary>
    public bool belowSignificance;
    /// <summary>那一次免費的重新編定用掉了沒。</summary>
    public bool redrawn;
    /// <summary>過了。過了的課永久凍結，它就是你過的那門課的紀錄。</summary>
    public bool passed;
    /// <summary>上一次應試的診斷，例如「第二曲・2′14″・容錯用盡」。</summary>
    public string lastAttempt = string.Empty;
}

/// <summary>檢定一級的考卷：技巧曲一首、歌唱曲一首，加上已經送審的成績。</summary>
[Serializable]
public sealed class SyllabusPaper
{
    public int grade;
    /// <summary>[0] 技巧曲、[1] 歌唱曲。</summary>
    public List<string> charts = new List<string>();
    public int widened;
    /// <summary>兩格送審。索引和 <see cref="charts"/> 對齊。</summary>
    public List<SyllabusSubmission> submissions = new List<SyllabusSubmission>();
    public bool awarded;
    public string seal = string.Empty;
}

/// <summary>送審的一局。四項的分數，外加那一局到底評到了什麼。</summary>
[Serializable]
public sealed class SyllabusSubmission
{
    public string chart = string.Empty;
    /// <summary>完整度、旋律、失誤錯音、情感表情。</summary>
    public float[] marks = new float[4];
    public int score;
    /// <summary>那一局真的有力度／踏板可評 —— 七八級要看這個，見 <see cref="SyllabusLadder.Grade.NeedsExpressionData"/>。</summary>
    public bool expressionMeasured;
    public long atUtcTicks;

    public float Average =>
        marks != null && marks.Length == 4
            ? (marks[0] + marks[1] + marks[2] + marks[3]) * 0.25f
            : 0f;
}

[Serializable]
internal sealed class SyllabusFile
{
    public int version = 1;
    /// <summary>持有技階，0 = 未敘階。</summary>
    public int stage;
    /// <summary>持有檢定級，0 = 未受檢。</summary>
    public int grade;
    /// <summary>持有級上蓋的印。只往上，見 <see cref="SyllabusLadder.SealRank"/>。</summary>
    public string seal = string.Empty;
    /// <summary>抽這一本時曲庫長什麼樣。曲庫變了才需要補抽。</summary>
    public string libraryStamp = string.Empty;
    public List<SyllabusCourse> courses = new List<SyllabusCourse>();
    public List<SyllabusPaper> papers = new List<SyllabusPaper>();
}

/// <summary>
/// 一位玩家的考程：他走到哪一階、哪一級，以及他那一本考卷長什麼樣。
/// </summary>
/// <remarks>
/// **為什麼整本一次抽完，而不是打開哪一階才抽那一階。** 兩個理由。第一，零紀錄的玩
/// 家打開考程書時眼前是 46 個<b>真的曲名</b>，不是 18 排灰掉的鎖 —— 這是這套系統對
/// 空狀態最大的一筆，而抽課引擎反正得寫，這個效果不加錢。第二也更重要：抽檢定考卷
/// 時會避開已經被用掉的曲目，如果是「打開才抽」，抽出來的東西就會取決於玩家<b>先打
/// 開哪一格</b>，同一個曲庫在兩台機器上會抽出兩本不同的書。一次抽完，順序才是固定
/// 的（技階一到十，然後檢定一到八）。
///
/// **為什麼存檔要走 <see cref="PlayerSlots.PathFor"/>。** 這台機器上有好幾個玩家，
/// 而他們的成績是分開的。寫進裸的 persistentDataPath 的話，兩個人共用同一本考程，
/// 而且 <c>PlayerSlots.Delete</c> 的檔案表不認得它、刪不掉。新增一份持久資料一定要
/// 做三件事：<c>PathFor</c>、把檔名加進 <c>PlayerSlots</c> 的 OwnedFiles、換人時丟
/// 掉靜態快取 —— 漏掉最後一件會把兩個人的檔案一起毀掉。
/// </remarks>
public static class SyllabusProgress
{
    public const string FileName = "syllabus.json";

    private static SyllabusFile data;

    /// <summary>考程有任何改變（抽了新的一本、授了階或級、送了審）時發出。</summary>
    public static event Action Changed;

    public static int Stage { get { EnsureLoaded(); return data.stage; } }
    public static int Grade { get { EnsureLoaded(); return data.grade; } }
    public static string Seal { get { EnsureLoaded(); return data.seal ?? string.Empty; } }

    public static IReadOnlyList<SyllabusCourse> Courses { get { EnsureLoaded(); return data.courses; } }
    public static IReadOnlyList<SyllabusPaper> Papers { get { EnsureLoaded(); return data.papers; } }

    /// <summary>一句話的現況，給牌面用。</summary>
    public static string Headline(bool recital)
    {
        EnsureLoaded();
        if (recital)
        {
            if (data.grade <= 0) return "未受檢";
            return string.IsNullOrEmpty(data.seal)
                ? $"{Numeral(data.grade)}級"
                : $"{Numeral(data.grade)}級　{data.seal}";
        }
        if (data.stage <= 0) return "未敘階";
        SyllabusLadder.Stage stage = SyllabusLadder.StageAt(data.stage);
        return stage != null ? $"{Numeral(data.stage)}階　{stage.Name}" : $"{Numeral(data.stage)}階";
    }

    /// <summary>
    /// 窄鈕用的短版：技階只印階數，不印技法名。
    /// </summary>
    /// <remarks>
    /// 練習室那兩顆鈕只有 148 寬，扣掉內距剩 132。「技階　六階　大跨度跳躍」在 16 級
    /// 下是 192 寬，自動縮下去只剩 11 級，讀不動。技法名在考程書上，那裡有的是地方。
    /// 檢定本來就短（最長「檢定　三級　優等」是 144），所以它照原樣。
    /// </remarks>
    public static string ShortHeadline(bool recital)
    {
        EnsureLoaded();
        if (recital) return Headline(true);
        return data.stage <= 0 ? "未敘階" : $"{Numeral(data.stage)}階";
    }

    /// <summary>下一步那一行。永遠說得出一件具體的事，或說清楚為什麼說不出。</summary>
    /// <remarks>
    /// 「還沒編定」和「曲庫真的不足」是兩件事，而它們在畫面上長得一樣（都是空的考
    /// 卷）。分開講：前者會自己好，後者要玩家去匯入譜面。
    /// </remarks>
    public static string NextStep(bool recital)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(data.libraryStamp)) return "考程編定中…";
        if (recital)
        {
            int next = data.grade + 1;
            SyllabusLadder.Grade grade = SyllabusLadder.GradeAt(next);
            if (grade == null) return "已達最高級";
            SyllabusPaper paper = PaperFor(next);
            if (paper == null || paper.charts == null || paper.charts.Count < SyllabusLadder.PaperSize)
                return $"曲庫不足：{Numeral(next)}級需 {SyllabusLadder.BandText(SyllabusOverrides.LowOf(grade), SyllabusOverrides.HighOf(grade))} 兩首不同曲目";
            if (data.stage < grade.RequiredStage)
                return $"{Numeral(next)}級需技階{Numeral(grade.RequiredStage)}階";
            int filled = FilledSlots(paper);
            return filled > 0
                ? $"{Numeral(next)}級　已送審 {filled} / {SyllabusLadder.PaperSize} 首"
                : $"下次應考：{Numeral(next)}級・{SyllabusLadder.PaperSize}曲";
        }

        int nextStage = data.stage + 1;
        SyllabusLadder.Stage stageDef = SyllabusLadder.StageAt(nextStage);
        if (stageDef == null) return "已達最高階";
        SyllabusCourse course = CourseFor(nextStage);
        if (course == null || course.charts == null || course.charts.Count < SyllabusLadder.CourseSize)
            return $"曲庫不足：{Numeral(nextStage)}階需 {SyllabusLadder.BandText(SyllabusOverrides.LowOf(stageDef), SyllabusOverrides.HighOf(stageDef))} 的三首不同曲目";
        // 課已經編好了，但還沒地方考。說出來，不要印一個按不下去的「下次應試」。
        if (!SyllabusLadder.ExamHallOpen) return "考場籌備中　課目已編定";
        return $"下次應試：{Numeral(nextStage)}階・{stageDef.Name}";
    }

    public static SyllabusCourse CourseFor(int stage)
    {
        EnsureLoaded();
        for (int i = 0; i < data.courses.Count; i++)
            if (data.courses[i] != null && data.courses[i].stage == stage) return data.courses[i];
        return null;
    }

    public static SyllabusPaper PaperFor(int grade)
    {
        EnsureLoaded();
        for (int i = 0; i < data.papers.Count; i++)
            if (data.papers[i] != null && data.papers[i].grade == grade) return data.papers[i];
        return null;
    }

    public static int FilledSlots(SyllabusPaper paper)
    {
        if (paper?.submissions == null) return 0;
        int filled = 0;
        for (int i = 0; i < paper.submissions.Count; i++)
            if (paper.submissions[i] != null && !string.IsNullOrEmpty(paper.submissions[i].chart)) filled++;
        return filled;
    }

    /// <summary>
    /// 把剛抽好的一整本寫進去，取代舊的那一本。
    /// </summary>
    /// <remarks>
    /// 已經過關的課和已經授級的考卷會被<b>保留</b>：那是紀錄，不是待辦。曲庫變動只該
    /// 影響你還沒做的事。
    /// </remarks>
    public static void AdoptDraft(List<SyllabusCourse> courses, List<SyllabusPaper> papers, string stamp)
    {
        EnsureLoaded();
        if (courses != null)
        {
            for (int i = 0; i < courses.Count; i++)
            {
                SyllabusCourse fresh = courses[i];
                if (fresh == null) continue;
                SyllabusCourse held = CourseFor(fresh.stage);
                if (held != null && held.passed) continue;          // 過了的課凍結
                if (held != null)
                {
                    // 重編的機會是玩家的，不會因為曲庫變動就還他
                    fresh.redrawn = held.redrawn;
                    fresh.lastAttempt = held.lastAttempt;
                    data.courses.Remove(held);
                }
                data.courses.Add(fresh);
            }
        }
        if (papers != null)
        {
            for (int i = 0; i < papers.Count; i++)
            {
                SyllabusPaper fresh = papers[i];
                if (fresh == null) continue;
                SyllabusPaper held = PaperFor(fresh.grade);
                if (held != null && held.awarded) continue;
                if (held != null)
                {
                    // 已經送審的那幾局只在曲目沒變的時候留著 —— 換了曲目，舊的送審
                    // 就是別的譜的成績，留著等於用 A 曲的分數換 B 曲的級。
                    if (SameCharts(held.charts, fresh.charts)) fresh.submissions = held.submissions;
                    data.papers.Remove(held);
                }
                data.papers.Add(fresh);
            }
        }
        data.courses.Sort((a, b) => a.stage.CompareTo(b.stage));
        data.papers.Sort((a, b) => a.grade.CompareTo(b.grade));
        data.libraryStamp = stamp ?? string.Empty;
        Reassess();
        Save();
        Changed?.Invoke();
    }

    /// <summary>
    /// 把每一張已經送滿的考卷重判一次。
    /// </summary>
    /// <remarks>
    /// 作者改過門檻之後，已經送滿兩格的考卷不會自己重判 —— <see cref="Evaluate"/> 只
    /// 在送審的那一刻跑。把八級的門檻從 85 調到 75，一個早就交完兩首的人卻要再交一
    /// 次才拿得到級數，那是說不通的。
    ///
    /// 由低到高跑：<see cref="Evaluate"/> 只授「持有級 +1」，所以一級授完二級才輪得
    /// 到，一次呼叫可以補完整條。這裡只會往上 —— 門檻調嚴不會把已經授出去的級收回
    /// 來，那是已經發生過的事。
    ///
    /// 掛在 <see cref="AdoptDraft"/> 尾巴而不是另外訂閱
    /// <c>SyllabusOverrides.Changed</c>：覆寫一改，指紋就變，整本一定會重編，所以這
    /// 裡是唯一保證「在新的曲目和新的門檻都就位之後」才跑的地方。
    /// </remarks>
    private static void Reassess()
    {
        for (int i = 1; i <= SyllabusLadder.Grades.Length; i++)
        {
            SyllabusPaper paper = PaperFor(i);
            if (paper == null || FilledSlots(paper) < paper.charts.Count) continue;
            Evaluate(paper);
        }
    }

    /// <summary>抽這一本的時候曲庫長什麼樣。不一樣就該重抽。</summary>
    public static bool StampMatches(string stamp) =>
        string.Equals(LibraryStamp, stamp ?? string.Empty, StringComparison.Ordinal);

    public static string LibraryStamp { get { EnsureLoaded(); return data.libraryStamp ?? string.Empty; } }

    /// <summary>
    /// 把一局演奏送審到某一級的某一格。
    /// </summary>
    /// <remarks>
    /// 送審永遠覆蓋那一格 —— 玩家按下「送審」就是要這一局，而不是要系統幫他比大小。
    /// 想留住比較好的那一局的人，看得到分數再決定按不按。
    /// </remarks>
    public static bool Submit(int grade, string chart, float[] marks, int score, bool expressionMeasured)
    {
        EnsureLoaded();
        SyllabusPaper paper = PaperFor(grade);
        if (paper == null || paper.charts == null || string.IsNullOrEmpty(chart)) return false;
        int slot = paper.charts.IndexOf(chart);
        if (slot < 0) return false;

        while (paper.submissions.Count < paper.charts.Count) paper.submissions.Add(null);
        var entry = new SyllabusSubmission
        {
            chart = chart,
            marks = marks != null && marks.Length == 4 ? (float[])marks.Clone() : new float[4],
            score = score,
            expressionMeasured = expressionMeasured,
            atUtcTicks = DateTime.UtcNow.Ticks,
        };
        paper.submissions[slot] = entry;
        Evaluate(paper);
        Save();
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// 兩格都滿了就判一次。判過了就授級、蓋印。
    /// </summary>
    /// <remarks>
    /// 只能授「持有級 +1」，所以回頭重考低階只會換印、不會跳級；而印只往上蓋。
    /// </remarks>
    private static void Evaluate(SyllabusPaper paper)
    {
        SyllabusLadder.Grade rules = SyllabusLadder.GradeAt(paper.grade);
        if (rules == null) return;
        // 一律讀覆寫過的值。書上印的也是這一份 —— 兩邊讀不同來源的話，會出現「書上
        // 寫 85、實際判 75」這種只有存檔看得出來的錯。
        SyllabusLadder.Demand demand = SyllabusOverrides.DemandOf(rules);
        bool needsExpression = SyllabusOverrides.NeedsExpressionOf(rules);
        if (FilledSlots(paper) < paper.charts.Count) return;
        if (data.stage < rules.RequiredStage) return;

        float sum = 0f;
        var totals = new float[4];
        for (int i = 0; i < paper.submissions.Count; i++)
        {
            SyllabusSubmission entry = paper.submissions[i];
            if (entry == null) return;
            if (needsExpression && !entry.expressionMeasured) return;
            for (int c = 0; c < 4; c++)
            {
                float need = demand.For(c);
                if (need > 0f && entry.marks[c] < need) return;
                totals[c] += entry.marks[c];
            }
            sum += entry.Average;
        }

        int count = paper.submissions.Count;
        for (int c = 0; c < 4; c++)
        {
            float need = demand.For(c);
            if (need > 0f && totals[c] / count < need) return;
        }
        float average = sum / count;
        if (demand.Average > 0f && average < demand.Average) return;

        string seal = SyllabusLadder.SealFor(average);
        if (string.IsNullOrEmpty(seal)) return;

        paper.awarded = true;
        if (SyllabusLadder.SealRank(seal) > SyllabusLadder.SealRank(paper.seal)) paper.seal = seal;

        if (paper.grade == data.grade + 1)
        {
            data.grade = paper.grade;
            data.seal = paper.seal;
        }
        else if (paper.grade == data.grade &&
                 SyllabusLadder.SealRank(paper.seal) > SyllabusLadder.SealRank(data.seal))
        {
            data.seal = paper.seal;
        }
    }

    /// <summary>過了一階。只認「持有階 +1」。</summary>
    public static void AwardStage(int stage)
    {
        EnsureLoaded();
        SyllabusCourse course = CourseFor(stage);
        if (course != null)
        {
            course.passed = true;
            course.lastAttempt = string.Empty;
        }
        if (stage == data.stage + 1) data.stage = stage;
        Save();
        Changed?.Invoke();
    }

    /// <summary>沒過。記的是診斷不是懲罰。</summary>
    public static void RecordAttempt(int stage, string diagnosis)
    {
        EnsureLoaded();
        SyllabusCourse course = CourseFor(stage);
        if (course == null) return;
        course.lastAttempt = diagnosis ?? string.Empty;
        Save();
        Changed?.Invoke();
    }

    /// <summary>那一次免費的重新編定。用掉就沒了。</summary>
    public static bool UseRedraw(int stage)
    {
        EnsureLoaded();
        SyllabusCourse course = CourseFor(stage);
        if (course == null || course.redrawn || course.passed) return false;
        course.redrawn = true;
        Save();
        Changed?.Invoke();
        return true;
    }

    // ------------------------------------------------------------------

    private static bool SameCharts(List<string> a, List<string> b)
    {
        if (a == null || b == null || a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (!string.Equals(a[i], b[i], StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    /// <summary>一到十的中文數字。牌面上「3階」看起來像版本號。</summary>
    public static string Numeral(int value)
    {
        switch (value)
        {
            case 1: return "一";
            case 2: return "二";
            case 3: return "三";
            case 4: return "四";
            case 5: return "五";
            case 6: return "六";
            case 7: return "七";
            case 8: return "八";
            case 9: return "九";
            case 10: return "十";
            default: return value.ToString();
        }
    }

    private static void EnsureLoaded()
    {
        if (data != null) return;
        string path = PlayerSlots.PathFor(FileName);
        try
        {
            data = File.Exists(path)
                ? JsonUtility.FromJson<SyllabusFile>(File.ReadAllText(path))
                : null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Syllabus] 考程讀不起來：{ex.Message}");
            data = null;
        }
        if (data == null) data = new SyllabusFile();
        // JsonUtility 把缺少的清單讀成 null，而這裡每一個都會被立刻走訪。
        if (data.courses == null) data.courses = new List<SyllabusCourse>();
        if (data.papers == null) data.papers = new List<SyllabusPaper>();
        data.courses.RemoveAll(c => c == null);
        data.papers.RemoveAll(p => p == null);
        for (int i = 0; i < data.courses.Count; i++)
            if (data.courses[i].charts == null) data.courses[i].charts = new List<string>();
        for (int i = 0; i < data.papers.Count; i++)
        {
            if (data.papers[i].charts == null) data.papers[i].charts = new List<string>();
            if (data.papers[i].submissions == null) data.papers[i].submissions = new List<SyllabusSubmission>();
            for (int s = 0; s < data.papers[i].submissions.Count; s++)
            {
                SyllabusSubmission entry = data.papers[i].submissions[s];
                if (entry != null && (entry.marks == null || entry.marks.Length != 4))
                    entry.marks = new float[4];
            }
        }
        data.stage = Mathf.Clamp(data.stage, 0, SyllabusLadder.Stages.Length);
        data.grade = Mathf.Clamp(data.grade, 0, SyllabusLadder.Grades.Length);
    }

    private static void Save()
    {
        string path = PlayerSlots.PathFor(FileName);
        string temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temporary, path);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Syllabus] 考程存不進去：{ex.Message}");
        }
    }

    /// <summary>換人了：丟掉快取，下一次讀就會讀到新那位的考程。</summary>
    /// <remarks>
    /// 靜態快取跨換人是這一整套最容易出的錯 —— 沒有丟的話，換過去的人會看到前一位的
    /// 階級，而且**下一次存檔會把它寫進新的人的檔案裡**，兩邊都毀掉。
    /// </remarks>
    private static void Forget()
    {
        data = null;
        Changed?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void HookSlotChanges() => PlayerSlots.Changed += Forget;
}
