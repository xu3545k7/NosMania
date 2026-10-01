using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>一階被改過的地方。0 / null / 空 一律代表「這一項沒改，照原本的」。</summary>
[Serializable]
public sealed class SyllabusStageTweak
{
    public int stage;
    public int low;
    public int high;
    /// <summary>容錯率。0 = 不覆寫。</summary>
    public float rate;
    /// <summary>釘住的曲目，長度 0 或 <see cref="SyllabusLadder.CourseSize"/>；空字串 = 這一格不釘。</summary>
    public List<string> pins = new List<string>();
}

/// <summary>一級被改過的地方。</summary>
[Serializable]
public sealed class SyllabusGradeTweak
{
    public int grade;
    public int low;
    public int high;
    /// <summary>完整度、旋律、失誤錯音、情感表情、兩首平均。null 或長度 ≠ 5 = 不覆寫。</summary>
    public float[] demand;
    /// <summary>−1 不覆寫、0 不需要、1 需要。</summary>
    public int expression = -1;
    public List<string> pins = new List<string>();
}

[Serializable]
internal sealed class SyllabusOverrideFile
{
    public int version = 1;
    /// <summary>改過幾次。進曲庫指紋，所以改完每位玩家下次進畫面都會重編。</summary>
    public int revision;
    public List<SyllabusStageTweak> stages = new List<SyllabusStageTweak>();
    public List<SyllabusGradeTweak> grades = new List<SyllabusGradeTweak>();
}

/// <summary>
/// 作者對考程的手改：等級帶、容錯率、四項門檻，以及釘死某一格要考哪一首。
/// </summary>
/// <remarks>
/// **為什麼這份檔案不在玩家資料夾裡。** 考程是這個遊戲的<b>內容</b>，不是某個人的紀
/// 錄。八級要求幾分、三階考哪三首，對這台機器上每一個人都必須是同一件事 —— 放進
/// <see cref="PlayerSlots"/> 的話，兩個人會考到不同的題目卻拿同一個名字的級數。所以
/// 它寫在 persistentDataPath 的根目錄，而<see cref="SyllabusProgress"/>（誰考到哪）
/// 仍然逐人一份。
///
/// **為什麼改動要記 revision 而不是直接改別人的存檔。** 改完之後每位玩家手上那一本
/// 都過期了，但我們不能替所有人重寫檔案（他們的進度不是我們的）。改成把 revision 併
/// 進曲庫指紋（<see cref="SyllabusCourses.LibraryStamp"/>）—— 每個人下次進畫面時自
/// 己發現對不上、自己重編，而已經過關的課和已經授級的考卷照樣保留。
///
/// **為什麼「沒改」是 0 而不是 −1。** JsonUtility 讀不到欄位時填的是預設值 0，所以
/// 用 0 當哨兵，舊檔和手改壞的檔都會自然地退回「沒改」。等級 0 本來就不是合法值。
/// 只有「需不需要表情資料」是布林，沒有這種空檔，才另外用 −1。
/// </remarks>
public static class SyllabusOverrides
{
    private const string FileName = "syllabus_overrides.json";

    private static SyllabusOverrideFile data;

    /// <summary>有人改了考程。抽課引擎和畫面都要跟著重來。</summary>
    public static event Action Changed;

    public static int Revision
    {
        get { EnsureLoaded(); return data.revision; }
    }

    /// <summary>整本有沒有被動過。沒有的話編輯器印「全部照自動編定」。</summary>
    public static bool Any
    {
        get { EnsureLoaded(); return data.stages.Count > 0 || data.grades.Count > 0; }
    }

    // ── 讀 ────────────────────────────────────────────────────────────

    public static int LowOf(SyllabusLadder.Stage stage)
    {
        SyllabusStageTweak tweak = FindStage(stage.Index, false);
        return tweak != null && tweak.low > 0 ? tweak.low : stage.Low;
    }

    public static int HighOf(SyllabusLadder.Stage stage)
    {
        SyllabusStageTweak tweak = FindStage(stage.Index, false);
        return tweak != null && tweak.high > 0 ? tweak.high : stage.High;
    }

    public static float RateOf(SyllabusLadder.Stage stage)
    {
        SyllabusStageTweak tweak = FindStage(stage.Index, false);
        return tweak != null && tweak.rate > 0f ? tweak.rate : stage.AllowanceRate;
    }

    public static int LowOf(SyllabusLadder.Grade grade)
    {
        SyllabusGradeTweak tweak = FindGrade(grade.Index, false);
        return tweak != null && tweak.low > 0 ? tweak.low : grade.Low;
    }

    public static int HighOf(SyllabusLadder.Grade grade)
    {
        SyllabusGradeTweak tweak = FindGrade(grade.Index, false);
        return tweak != null && tweak.high > 0 ? tweak.high : grade.High;
    }

    public static SyllabusLadder.Demand DemandOf(SyllabusLadder.Grade grade)
    {
        SyllabusGradeTweak tweak = FindGrade(grade.Index, false);
        if (tweak?.demand == null || tweak.demand.Length != 5) return grade.Demand;
        return SyllabusLadder.MakeDemand(tweak.demand[0], tweak.demand[1], tweak.demand[2],
            tweak.demand[3], tweak.demand[4]);
    }

    public static bool NeedsExpressionOf(SyllabusLadder.Grade grade)
    {
        SyllabusGradeTweak tweak = FindGrade(grade.Index, false);
        return tweak != null && tweak.expression >= 0
            ? tweak.expression != 0
            : grade.NeedsExpressionData;
    }

    /// <summary>某一格被釘住的曲目，沒釘回空字串。</summary>
    public static string PinOf(bool recital, int index, int slot)
    {
        List<string> pins = recital ? FindGrade(index, false)?.pins : FindStage(index, false)?.pins;
        return pins != null && slot >= 0 && slot < pins.Count ? pins[slot] ?? string.Empty : string.Empty;
    }

    public static bool IsTweaked(bool recital, int index) =>
        recital ? FindGrade(index, false) != null : FindStage(index, false) != null;

    // ── 寫 ────────────────────────────────────────────────────────────

    public static void SetBand(bool recital, int index, int low, int high)
    {
        // 上下顛倒的帶子抽不出任何東西，而使用者是一格一格點的，中途一定會顛倒。
        if (low > high) (low, high) = (high, low);
        if (recital)
        {
            SyllabusGradeTweak tweak = FindGrade(index, true);
            tweak.low = low;
            tweak.high = high;
        }
        else
        {
            SyllabusStageTweak tweak = FindStage(index, true);
            tweak.low = low;
            tweak.high = high;
        }
        Commit();
    }

    public static void SetRate(int stage, float rate)
    {
        FindStage(stage, true).rate = Mathf.Clamp(rate, 0.001f, 0.2f);
        Commit();
    }

    public static void SetDemand(int grade, float[] demand)
    {
        if (demand == null || demand.Length != 5) return;
        SyllabusGradeTweak tweak = FindGrade(grade, true);
        tweak.demand = (float[])demand.Clone();
        for (int i = 0; i < 5; i++) tweak.demand[i] = Mathf.Clamp(tweak.demand[i], 0f, 100f);
        Commit();
    }

    public static void SetExpression(int grade, bool needs)
    {
        FindGrade(grade, true).expression = needs ? 1 : 0;
        Commit();
    }

    /// <summary>釘死（或放開）某一格的曲目。<paramref name="chart"/> 空字串 = 放開。</summary>
    public static void SetPin(bool recital, int index, int slot, string chart)
    {
        int size = recital ? SyllabusLadder.PaperSize : SyllabusLadder.CourseSize;
        if (slot < 0 || slot >= size) return;
        List<string> pins = recital ? FindGrade(index, true).pins : FindStage(index, true).pins;
        while (pins.Count < size) pins.Add(string.Empty);
        // 同一首不能釘在同一列的兩格：抽課本來就保證三首不同，釘出兩格一樣的話那一
        // 列就再也湊不滿，而畫面上看起來只是「有一格怪怪的」。
        for (int i = 0; i < pins.Count; i++)
            if (i != slot && string.Equals(pins[i], chart, StringComparison.OrdinalIgnoreCase))
                pins[i] = string.Empty;
        pins[slot] = chart ?? string.Empty;
        Commit();
    }

    public static void ResetRow(bool recital, int index)
    {
        EnsureLoaded();
        if (recital) data.grades.RemoveAll(g => g != null && g.grade == index);
        else data.stages.RemoveAll(s => s != null && s.stage == index);
        Commit();
    }

    public static void ResetAll()
    {
        EnsureLoaded();
        data.stages.Clear();
        data.grades.Clear();
        Commit();
    }

    // ------------------------------------------------------------------

    private static SyllabusStageTweak FindStage(int index, bool create)
    {
        EnsureLoaded();
        for (int i = 0; i < data.stages.Count; i++)
            if (data.stages[i] != null && data.stages[i].stage == index) return data.stages[i];
        if (!create) return null;
        var fresh = new SyllabusStageTweak { stage = index };
        data.stages.Add(fresh);
        return fresh;
    }

    private static SyllabusGradeTweak FindGrade(int index, bool create)
    {
        EnsureLoaded();
        for (int i = 0; i < data.grades.Count; i++)
            if (data.grades[i] != null && data.grades[i].grade == index) return data.grades[i];
        if (!create) return null;
        var fresh = new SyllabusGradeTweak { grade = index };
        data.grades.Add(fresh);
        return fresh;
    }

    private static void Commit()
    {
        data.revision++;
        Save();
        Changed?.Invoke();
    }

    private static void EnsureLoaded()
    {
        if (data != null) return;
        string path = Path.Combine(Application.persistentDataPath, FileName);
        try
        {
            data = File.Exists(path)
                ? JsonUtility.FromJson<SyllabusOverrideFile>(File.ReadAllText(path))
                : null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Syllabus] 考程覆寫讀不起來：{ex.Message}");
            data = null;
        }
        if (data == null) data = new SyllabusOverrideFile();
        if (data.stages == null) data.stages = new List<SyllabusStageTweak>();
        if (data.grades == null) data.grades = new List<SyllabusGradeTweak>();
        data.stages.RemoveAll(s => s == null);
        data.grades.RemoveAll(g => g == null);
        for (int i = 0; i < data.stages.Count; i++)
            if (data.stages[i].pins == null) data.stages[i].pins = new List<string>();
        for (int i = 0; i < data.grades.Count; i++)
            if (data.grades[i].pins == null) data.grades[i].pins = new List<string>();
    }

    private static void Save()
    {
        string path = Path.Combine(Application.persistentDataPath, FileName);
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
            Debug.LogWarning($"[Syllabus] 考程覆寫存不進去：{ex.Message}");
        }
    }
}
