using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
internal sealed class PlayerSlotFile
{
    public int version = 1;
    public string active;
    public List<string> ids = new List<string>();
}

/// <summary>
/// 好幾個玩家共用一台電腦：每個人有自己的資料夾，資料互不相干。
/// </summary>
/// <remarks>
/// **為什麼加一層而不是在每份檔案裡塞一個玩家欄位。** 這台機器上有三份各自獨立的持
/// 久資料（`PlayerProfile`、`PlayerRating`、`LocalScoreRecords`），之後還會有段位和檢
/// 定。如果每一份都自己處理「這是誰的」，那就是五個地方各寫一次同樣的分流邏輯，而且
/// 每加一份資料就要再寫一次。改成換一個**目錄**，那五份檔案的格式一個字都不用動：
///
/// <code>
/// persistentDataPath/
///   players.json                       ← 誰在玩、有哪些人
///   players/&lt;id&gt;/player_profile.json
///   players/&lt;id&gt;/player_portrait.png
///   players/&lt;id&gt;/player_rating.json
///   players/&lt;id&gt;/nostalgia_local_scores.json
/// </code>
///
/// **舊資料會被搬進第一個 slot，不是被丟掉。** 這個功能是後加的，而使用者機器上已經
/// 有成績了。第一次啟動時如果根目錄還有舊檔，就整批搬進 slot 1 —— 搬不動（檔案被鎖、
/// 權限不足）就退回複製，再不行就放著不動並且記一筆警告，**絕不刪除**。
///
/// **名字不存在這裡。** 每個 slot 的名字和火漆印是它自己 `player_profile.json` 裡的，
/// 這裡只記 id 和誰是現在這個。重複存一份名字的話，改名就要改兩個地方，而那兩個地方
/// 遲早會不一致。切換畫面要列出所有人的時候再去讀各自的那一份 —— 玩家不會有幾十個。
/// </remarks>
public static class PlayerSlots
{
    private const string IndexFileName = "players.json";
    private const string SlotFolder = "players";

    /// <summary>會跟著 slot 走的那幾份檔案。搬家和刪除都照這張表。</summary>
    private static readonly string[] OwnedFiles =
    {
        "player_profile.json",
        "player_portrait.png",
        "player_rating.json",
        "nostalgia_local_scores.json",
        SyllabusProgress.FileName
    };

    private static PlayerSlotFile index;

    /// <summary>換人之後每一份快取都要重讀。訂閱者自己負責丟掉自己的快取。</summary>
    public static event Action Changed;

    public static string ActiveId
    {
        get
        {
            EnsureLoaded();
            return index.active;
        }
    }

    public static IReadOnlyList<string> Ids
    {
        get
        {
            EnsureLoaded();
            return index.ids;
        }
    }

    /// <summary>
    /// 某一份持久檔案現在該讀寫哪個路徑。三個系統只要把 `Path.Combine` 換成這個。
    /// </summary>
    public static string PathFor(string fileName)
    {
        EnsureLoaded();
        string folder = Path.Combine(Application.persistentDataPath, SlotFolder, index.active);
        try { Directory.CreateDirectory(folder); } catch { }
        return Path.Combine(folder, fileName);
    }

    /// <summary>新開一個人。回傳它的 id，並且**立刻切過去**。</summary>
    /// <remarks>
    /// id 用時間戳而不是流水號：流水號在刪掉中間某一個之後會被重用，而舊的資料夾要是
    /// 因為任何原因沒刪乾淨，新的人就會撿到別人的成績。
    /// </remarks>
    public static string Create()
    {
        EnsureLoaded();
        string id = DateTime.UtcNow.Ticks.ToString("x");
        if (index.ids.Contains(id)) id += "-" + UnityEngine.Random.Range(0x1000, 0xffff).ToString("x");
        index.ids.Add(id);
        index.active = id;
        Save();
        try { Directory.CreateDirectory(Path.Combine(Application.persistentDataPath, SlotFolder, id)); }
        catch (Exception ex) { Debug.LogWarning($"[PlayerSlots] 建不出資料夾：{ex.Message}"); }
        Changed?.Invoke();
        return id;
    }

    /// <summary>換人。id 不存在就什麼都不做。</summary>
    public static bool Switch(string id)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(id) || !index.ids.Contains(id)) return false;
        if (string.Equals(index.active, id, StringComparison.Ordinal)) return true;
        index.active = id;
        Save();
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// 刪掉一個人，連同他的資料夾。最後一個不能刪。
    /// </summary>
    /// <remarks>
    /// 這是這個檔案裡唯一會毀掉資料的動作，所以它只做被明確要求的那一件事：刪掉那個
    /// 資料夾裡**我們認得的那幾份檔案**，再試著刪空目錄。使用者自己放進去的東西不碰，
    /// 目錄刪不掉就留著 —— 一個空目錄無害，誤刪別人的檔案不是。
    /// </remarks>
    public static bool Delete(string id)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(id) || !index.ids.Contains(id)) return false;
        if (index.ids.Count <= 1) return false;

        string folder = Path.Combine(Application.persistentDataPath, SlotFolder, id);
        foreach (string owned in OwnedFiles)
        {
            try
            {
                string path = Path.Combine(folder, owned);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex) { Debug.LogWarning($"[PlayerSlots] 刪不掉 {owned}：{ex.Message}"); }
        }
        try { if (Directory.Exists(folder)) Directory.Delete(folder, false); } catch { }

        index.ids.Remove(id);
        if (string.Equals(index.active, id, StringComparison.Ordinal)) index.active = index.ids[0];
        Save();
        Changed?.Invoke();
        return true;
    }

    /// <summary>某一個人的顯示名稱，給切換畫面用。讀的是那個 slot 自己的檔案。</summary>
    public static string NameOf(string id)
    {
        if (string.IsNullOrEmpty(id)) return string.Empty;
        string path = Path.Combine(Application.persistentDataPath, SlotFolder, id, "player_profile.json");
        try
        {
            if (!File.Exists(path)) return string.Empty;
            // 只要名字，不要為了一個字串把整個 ProfileFile 的型別耦合進來。
            string json = File.ReadAllText(path);
            const string key = "\"name\":\"";
            int at = json.IndexOf(key, StringComparison.Ordinal);
            if (at < 0) return string.Empty;
            at += key.Length;
            int end = json.IndexOf('"', at);
            return end > at ? json.Substring(at, end - at) : string.Empty;
        }
        catch { return string.Empty; }
    }

    // ------------------------------------------------------------------

    private static void EnsureLoaded()
    {
        if (index != null) return;
        string path = Path.Combine(Application.persistentDataPath, IndexFileName);
        try
        {
            index = File.Exists(path)
                ? JsonUtility.FromJson<PlayerSlotFile>(File.ReadAllText(path))
                : null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PlayerSlots] 名冊讀不起來：{ex.Message}");
            index = null;
        }

        if (index == null) index = new PlayerSlotFile();
        if (index.ids == null) index.ids = new List<string>();
        index.ids.RemoveAll(string.IsNullOrEmpty);

        bool fresh = index.ids.Count == 0;
        if (fresh)
        {
            index.ids.Add("1");
            index.active = "1";
        }
        if (string.IsNullOrEmpty(index.active) || !index.ids.Contains(index.active))
            index.active = index.ids[0];

        if (fresh)
        {
            AdoptLegacyFiles(index.active);
            Save();
        }
    }

    /// <summary>
    /// 把舊版放在根目錄的那幾份檔案搬進第一個 slot。
    /// </summary>
    /// <remarks>
    /// 搬不動就複製，複製也失敗就放著 —— 這個函式**永遠不刪除來源**。使用者機器上已
    /// 經有成績了，而一次失敗的搬家寧可留下兩份，也不要留下零份。
    /// </remarks>
    private static void AdoptLegacyFiles(string id)
    {
        string folder = Path.Combine(Application.persistentDataPath, SlotFolder, id);
        try { Directory.CreateDirectory(folder); }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PlayerSlots] 建不出第一個資料夾，舊資料留在原處：{ex.Message}");
            return;
        }

        int moved = 0;
        foreach (string owned in OwnedFiles)
        {
            string from = Path.Combine(Application.persistentDataPath, owned);
            string to = Path.Combine(folder, owned);
            try
            {
                if (!File.Exists(from) || File.Exists(to)) continue;
                File.Move(from, to);
                moved++;
            }
            catch
            {
                try
                {
                    File.Copy(from, to, false);
                    moved++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlayerSlots] {owned} 搬不進 slot：{ex.Message}");
                }
            }
        }
        if (moved > 0) Debug.Log($"[PlayerSlots] 舊資料 {moved} 份已收進第一位玩家。");
    }

    private static void Save()
    {
        string path = Path.Combine(Application.persistentDataPath, IndexFileName);
        try
        {
            File.WriteAllText(path, JsonUtility.ToJson(index, true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PlayerSlots] 名冊寫不進去：{ex.Message}");
        }
    }
}
