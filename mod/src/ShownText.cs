using System.Collections.Generic;

namespace ProbablyStolenZhHant
{
    /// <summary>
    /// 記住每個文字物件「畫面上的轉換結果 → 原文」，遊戲或其他 mod 讀取文字時換回原文。
    /// 「只在顯示時轉換」延伸到讀取：程式邏輯永遠看到簡中原文，只有畫面是繁體。
    /// 起因（2026-10-05）：Enhanced Trade Display 每幀讀回交易面板的價格文字，沒找到自己接過的「预计赚」就再接一次；
    /// 讀到轉換後的「預計賺」就以為沒接過，一直重複接，文字忽長忽短、自動縮字，看起來在亂跳。
    /// 不碰 Il2Cpp 型別（物件用指標當 id），離線驗證（mod/verify）可以直接測。
    /// </summary>
    public sealed class ShownText
    {
        readonly Dictionary<long, (string shown, string original)> _map = new Dictionary<long, (string, string)>();

        /// <summary>紀錄上限。物件銷毀後紀錄不會自己消失，超過就整個清掉；最壞只是那些物件暫時讀到繁體（加這個功能前的行為）。</summary>
        public int MaxEntries = 20000;
        public long Reads, Restored, Clears;
        public int Count => _map.Count;

        /// <summary>設定文字時呼叫：轉換結果和原文不同才記；相同（不用轉、或設進來的已經是繁體）就清掉舊紀錄。</summary>
        public void Record(long id, string original, string shown)
        {
            if (original == null || shown == null || original == shown) { _map.Remove(id); return; }
            if (_map.Count >= MaxEntries && !_map.ContainsKey(id)) { _map.Clear(); Clears++; }
            _map[id] = (shown, original);
        }

        /// <summary>
        /// 讀取文字時呼叫：讀到的正是記下的轉換結果才換回原文。
        /// 物件銷毀後指標可能被新物件沿用，但內容也一樣才會換，換回的原文仍然正確。
        /// </summary>
        public string Restore(long id, string current)
        {
            Reads++;
            if (current != null && _map.TryGetValue(id, out var r) && r.shown == current) { Restored++; return r.original; }
            return current;
        }
    }
}
