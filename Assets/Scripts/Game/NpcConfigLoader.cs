using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace POELike.Game
{
    /// <summary>
    /// NPC配置加载器
    /// 从 Assets/Cfg/ 读取 NPCDialogConf.pb、NPCButtonConf.pb、ButtonDataConf.pb
    /// 提供根据 NPCID 查询对话内容和按钮列表的接口
    /// </summary>
    public static class NpcConfigLoader
    {
        // ── 数据模型 ──────────────────────────────────────────────────

        [Serializable] private class NpcDialogWrapper    { public List<NpcDialogData>    NPCDialogConf;  }
        [Serializable] private class NpcButtonWrapper    { public List<NpcButtonData>    NPCButtonConf;  }
        [Serializable] private class ButtonDataWrapper   { public List<ButtonData>       ButtonDataConf; }

        [Serializable]
        private class NpcDialogData
        {
            public string NPCID;
            public string DialogContent;
        }

        [Serializable]
        private class NpcButtonData
        {
            public string NPCID;
            public List<int> ButtonIDs;
        }

        [Serializable]
        private class ButtonData
        {
            public string ButtonID;
            public string EventID;
            public string ButtonName;
        }

        // ── 缓存 ──────────────────────────────────────────────────────

        private static Dictionary<int, string>       _dialogMap;
        private static Dictionary<int, List<int>>    _npcButtonMap;
        private static Dictionary<int, ButtonData>   _buttonDataMap;

        // ── 公开接口 ──────────────────────────────────────────────────

        /// <summary>
        /// 根据 NPCID 获取对话内容，找不到时返回默认文本
        /// </summary>
        public static string GetDialog(int npcId)
        {
            EnsureLoaded();
            return _dialogMap.TryGetValue(npcId, out var text) ? text : "……";
        }

        /// <summary>
        /// 根据 NPCID 获取按钮列表（名称 + EventID）
        /// </summary>
        public static List<(string name, string eventId)> GetButtons(int npcId)
        {
            EnsureLoaded();
            var result = new List<(string, string)>();
            if (!_npcButtonMap.TryGetValue(npcId, out var ids)) return result;
            foreach (var bid in ids)
            {
                if (_buttonDataMap.TryGetValue(bid, out var btn))
                    result.Add((btn.ButtonName, btn.EventID));
            }
            return result;
        }

        /// <summary>
        /// 强制重新加载所有配置（热重载用）
        /// </summary>
        public static void Reload()
        {
            _dialogMap     = null;
            _npcButtonMap  = null;
            _buttonDataMap = null;
            EnsureLoaded();
        }

        // ── 私有加载逻辑 ──────────────────────────────────────────────

        private static void EnsureLoaded()
        {
            if (_dialogMap != null) return;
            _dialogMap     = new Dictionary<int, string>();
            _npcButtonMap  = new Dictionary<int, List<int>>();
            _buttonDataMap = new Dictionary<int, ButtonData>();

            LoadDialogConf();
            LoadButtonConf();
            LoadButtonData();
        }

        private static string ReadCfg(string fileName)
        {
            // 优先从 Resources/Cfg 加载
            string resName = Path.GetFileNameWithoutExtension(fileName);
            var asset = Resources.Load<TextAsset>($"Cfg/{resName}");
            if (asset != null) return asset.text;

            // 回退到 Assets/Cfg/
            string path = Path.Combine(Application.dataPath, "Cfg", fileName);
            if (File.Exists(path)) return File.ReadAllText(path);

            Debug.LogWarning($"[NpcConfigLoader] 找不到配置文件: {fileName}");
            return null;
        }

        private static void LoadDialogConf()
        {
            var json = ReadCfg("NPCDialogConf.pb");
            if (json == null) return;
            try
            {
                var wrapper = JsonUtility.FromJson<NpcDialogWrapper>(json);
                foreach (var d in wrapper?.NPCDialogConf ?? new List<NpcDialogData>())
                    if (int.TryParse(d.NPCID, out int id))
                        _dialogMap[id] = d.DialogContent;
            }
            catch (Exception e) { Debug.LogError($"[NpcConfigLoader] 解析NPCDialogConf失败: {e.Message}"); }
        }

        private static void LoadButtonConf()
        {
            var json = ReadCfg("NPCButtonConf.pb");
            if (json == null) return;
            try
            {
                var wrapper = JsonUtility.FromJson<NpcButtonWrapper>(json);
                foreach (var d in wrapper?.NPCButtonConf ?? new List<NpcButtonData>())
                    if (int.TryParse(d.NPCID, out int id))
                        _npcButtonMap[id] = d.ButtonIDs ?? new List<int>();
            }
            catch (Exception e) { Debug.LogError($"[NpcConfigLoader] 解析NPCButtonConf失败: {e.Message}"); }
        }

        private static void LoadButtonData()
        {
            var json = ReadCfg("ButtonDataConf.pb");
            if (json == null) return;
            try
            {
                var wrapper = JsonUtility.FromJson<ButtonDataWrapper>(json);
                foreach (var d in wrapper?.ButtonDataConf ?? new List<ButtonData>())
                    if (int.TryParse(d.ButtonID, out int id))
                        _buttonDataMap[id] = d;
            }
            catch (Exception e) { Debug.LogError($"[NpcConfigLoader] 解析ButtonDataConf失败: {e.Message}"); }
        }
    }
}
