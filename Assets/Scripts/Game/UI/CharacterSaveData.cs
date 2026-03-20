using System;
using System.Collections.Generic;
using UnityEngine;

namespace POELike.Game.UI
{
    /// <summary>
    /// 单个角色的存档数据
    /// </summary>
    [Serializable]
    public class CharacterSaveData
    {
        /// <summary>唯一存档ID</summary>
        public string SaveId;
        /// <summary>角色名称</summary>
        public string CharacterName;
        /// <summary>角色等级</summary>
        public int Level;
        /// <summary>职业/区服名称</summary>
        public string ServerName;
        /// <summary>头像图片路径（Resources相对路径）</summary>
        public string AvatarPath;
        /// <summary>最后游玩时间</summary>
        public string LastPlayTime;
        /// <summary>游戏时长（分钟）</summary>
        public int PlayTimeMinutes;

        public CharacterSaveData() { }

        public CharacterSaveData(string saveId, string name, int level, string server, string avatarPath = "")
        {
            SaveId = saveId;
            CharacterName = name;
            Level = level;
            ServerName = server;
            AvatarPath = avatarPath;
            LastPlayTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            PlayTimeMinutes = 0;
        }
    }

    /// <summary>
    /// 存档管理器（静态工具类）
    /// 负责存档的增删改查，数据持久化使用 PlayerPrefs
    /// </summary>
    public static class CharacterSaveManager
    {
        private const string SaveListKey = "POELike_SaveList";
        private const string SavePrefix   = "POELike_Save_";
        private const int    MaxSlots     = 10;

        // ── 读取所有存档 ──────────────────────────────────────────────
        public static List<CharacterSaveData> LoadAllSaves()
        {
            var result = new List<CharacterSaveData>();
            string raw = PlayerPrefs.GetString(SaveListKey, "");
            if (string.IsNullOrEmpty(raw)) return result;

            foreach (var id in raw.Split(','))
            {
                if (string.IsNullOrEmpty(id)) continue;
                string json = PlayerPrefs.GetString(SavePrefix + id, "");
                if (string.IsNullOrEmpty(json)) continue;
                try
                {
                    var data = JsonUtility.FromJson<CharacterSaveData>(json);
                    if (data != null) result.Add(data);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[CharacterSaveManager] 读取存档失败 id={id}: {e.Message}");
                }
            }
            return result;
        }

        // ── 保存单个存档 ──────────────────────────────────────────────
        public static void SaveCharacter(CharacterSaveData data)
        {
            if (data == null || string.IsNullOrEmpty(data.SaveId)) return;

            PlayerPrefs.SetString(SavePrefix + data.SaveId, JsonUtility.ToJson(data));

            // 更新 ID 列表
            var ids = GetSaveIdList();
            if (!ids.Contains(data.SaveId))
                ids.Add(data.SaveId);
            PlayerPrefs.SetString(SaveListKey, string.Join(",", ids));
            PlayerPrefs.Save();
        }

        // ── 删除存档 ──────────────────────────────────────────────────
        public static void DeleteCharacter(string saveId)
        {
            if (string.IsNullOrEmpty(saveId)) return;

            PlayerPrefs.DeleteKey(SavePrefix + saveId);

            var ids = GetSaveIdList();
            ids.Remove(saveId);
            PlayerPrefs.SetString(SaveListKey, string.Join(",", ids));
            PlayerPrefs.Save();
        }

        // ── 是否还能创建新角色 ────────────────────────────────────────
        public static bool CanCreateNew() => GetSaveIdList().Count < MaxSlots;

        // ── 生成新存档ID ──────────────────────────────────────────────
        public static string GenerateNewId() => Guid.NewGuid().ToString("N")[..8];

        // ── 内部：获取 ID 列表 ────────────────────────────────────────
        private static List<string> GetSaveIdList()
        {
            string raw = PlayerPrefs.GetString(SaveListKey, "");
            var list = new List<string>();
            if (string.IsNullOrEmpty(raw)) return list;
            foreach (var id in raw.Split(','))
                if (!string.IsNullOrEmpty(id)) list.Add(id);
            return list;
        }

#if UNITY_EDITOR
        /// <summary>编辑器用：写入几条测试存档</summary>
        public static void CreateTestSaves()
        {
            SaveCharacter(new CharacterSaveData(GenerateNewId(), "暗影刺客",  42, "亚服-S1"));
            SaveCharacter(new CharacterSaveData(GenerateNewId(), "烈焰法师",  18, "国服-S3"));
            SaveCharacter(new CharacterSaveData(GenerateNewId(), "钢铁战士",   7, "国服-S1"));
            Debug.Log("[CharacterSaveManager] 测试存档已写入");
        }
#endif
    }
}
