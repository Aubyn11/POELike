using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using POELike.Game;

namespace POELike.Editor
{
    /// <summary>
    /// NPC Mesh 同步工具
    /// 菜单：POELike → NPC → 同步 NPC Mesh 到 Resources
    ///
    /// 功能：
    ///   1. 读取 Assets/Cfg/NPCDataConf.pb，提取所有 NPCMesh 字段（去重）
    ///   2. 从 TT_RTS 预制体目录找到对应预制体
    ///   3. 提取预制体中所有激活的 MeshFilter + MeshRenderer
    ///   4. 生成轻量的 NpcMeshBundle（ScriptableObject）存入 Resources/Prefabs/
    ///      只保留 Mesh + Material 引用，不含动画/碰撞体等冗余数据
    ///   5. 删除 Resources/Prefabs/ 中不再被配置引用的旧 Bundle（自动清理）
    /// </summary>
    public static class NpcMeshSync
    {
        // ── 路径常量 ──────────────────────────────────────────────────
        private const string ConfigPath   = "Assets/Cfg/NPCDataConf.pb";
        private const string SrcPrefabDir = "Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/prefabs";
        private const string DstDir       = "Assets/Resources/Prefabs";

        // ── 菜单入口 ──────────────────────────────────────────────────

        [MenuItem("POELike/NPC/同步 NPC Mesh 到 Resources（运行前执行）")]
        public static void SyncNpcMeshToResources()
        {
            // ── 1. 读取配置，提取 NPCMesh 字段 ───────────────────────
            var meshNames = ReadNpcMeshNames();
            if (meshNames == null || meshNames.Count == 0)
            {
                EditorUtility.DisplayDialog("NPC Mesh 同步", "NPCDataConf.pb 中没有找到任何 NPCMesh 配置。", "确定");
                return;
            }

            // ── 2. 确保目标目录存在 ───────────────────────────────────
            EnsureDirectory("Assets/Resources");
            EnsureDirectory(DstDir);

            // ── 3. 逐个提取并生成 NpcMeshBundle ──────────────────────
            int successCount = 0;
            int skipCount    = 0;
            var errors       = new List<string>();

            foreach (var meshName in meshNames)
            {
                try
                {
                    bool created = ExtractAndSaveBundle(meshName);
                    if (created) successCount++;
                    else         skipCount++;
                }
                catch (Exception e)
                {
                    errors.Add($"[{meshName}] {e.Message}");
                    Debug.LogError($"[NpcMeshSync] 提取 {meshName} 失败: {e}");
                }
            }

            // ── 4. 清理不再使用的旧 Bundle ────────────────────────────
            int cleanCount = CleanObsoleteBundles(meshNames);

            // ── 5. 刷新 AssetDatabase ─────────────────────────────────
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── 6. 显示结果 ───────────────────────────────────────────
            string msg = $"同步完成！\n\n" +
                         $"✅ 新建/更新：{successCount} 个\n" +
                         $"⏭ 跳过（已是最新）：{skipCount} 个\n" +
                         $"🗑 清理旧 Bundle：{cleanCount} 个";

            if (errors.Count > 0)
                msg += $"\n\n❌ 失败 {errors.Count} 个：\n" + string.Join("\n", errors);

            EditorUtility.DisplayDialog("NPC Mesh 同步", msg, "确定");
            Debug.Log($"[NpcMeshSync] {msg}");
        }

        // ── 核心逻辑 ──────────────────────────────────────────────────

        /// <summary>
        /// 从 NPCDataConf.pb 读取所有不重复的 NPCMesh 名称
        /// </summary>
        private static HashSet<string> ReadNpcMeshNames()
        {
            if (!File.Exists(ConfigPath))
            {
                Debug.LogError($"[NpcMeshSync] 找不到配置文件: {ConfigPath}");
                return null;
            }

            string json = File.ReadAllText(ConfigPath);
            NPCDataWrapper wrapper;
            try
            {
                wrapper = JsonUtility.FromJson<NPCDataWrapper>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NpcMeshSync] 解析 NPCDataConf.pb 失败: {e.Message}");
                return null;
            }

            var result = new HashSet<string>(StringComparer.Ordinal);
            if (wrapper?.NPCDataConf == null) return result;

            foreach (var npc in wrapper.NPCDataConf)
            {
                if (!string.IsNullOrEmpty(npc.NPCMesh))
                    result.Add(npc.NPCMesh.Trim());
            }

            Debug.Log($"[NpcMeshSync] 从配置中读取到 {result.Count} 种 NPC Mesh: {string.Join(", ", result)}");
            return result;
        }

        /// <summary>
        /// 从源预制体提取 Mesh + Material，生成或更新 NpcMeshBundle。
        /// 返回 true 表示新建/更新，false 表示已是最新跳过。
        /// </summary>
        private static bool ExtractAndSaveBundle(string meshName)
        {
            string srcPath = $"{SrcPrefabDir}/{meshName}.prefab";
            string dstPath = $"{DstDir}/{meshName}_Bundle.asset";

            // 检查源预制体是否存在
            var srcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
            if (srcPrefab == null)
                throw new FileNotFoundException($"找不到源预制体: {srcPath}");

            // 实例化预制体（BakeMesh 需要在实例化对象上调用）
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(srcPrefab);
            instance.hideFlags = HideFlags.HideAndDontSave;

            List<NpcMeshPart> parts;
            try
            {
                parts = ExtractMeshParts(instance);
            }
            finally
            {
                GameObject.DestroyImmediate(instance);
            }

            if (parts.Count == 0)
            {
                Debug.LogWarning($"[NpcMeshSync] 预制体 [{meshName}] 中没有找到任何激活的渲染部件，跳过。");
                return false;
            }

            // 加载或创建 Bundle
            var bundle = AssetDatabase.LoadAssetAtPath<NpcMeshBundle>(dstPath);
            bool isNew = (bundle == null);

            if (isNew)
            {
                bundle = ScriptableObject.CreateInstance<NpcMeshBundle>();
                bundle.PrefabName = meshName;
                AssetDatabase.CreateAsset(bundle, dstPath);
            }

            // 删除旧的子资产 Mesh（避免重复堆积）
            var existingSubAssets = AssetDatabase.LoadAllAssetsAtPath(dstPath);
            foreach (var sub in existingSubAssets)
            {
                if (sub is Mesh)
                    AssetDatabase.RemoveObjectFromAsset(sub);
            }

            // 将烘焙出的 Mesh 保存为 Bundle 的子资产（SkinnedMesh 烘焙结果需要持久化）
            foreach (var part in parts)
            {
                if (part.Mesh != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(part.Mesh)))
                {
                    part.Mesh.name = part.PartName + "_Baked";
                    AssetDatabase.AddObjectToAsset(part.Mesh, dstPath);
                }
            }

            // 写入数据
            bundle.PrefabName = meshName;
            bundle.Parts      = parts;
            EditorUtility.SetDirty(bundle);

            Debug.Log($"[NpcMeshSync] [{meshName}] {(isNew ? "新建" : "更新")} Bundle，共 {parts.Count} 个部件 → {dstPath}");
            return true;
        }

        /// <summary>
        /// 从预制体中提取所有激活的渲染部件：
        ///   - MeshFilter + MeshRenderer：直接使用 sharedMesh，LocalMatrix 为相对根节点的变换
        ///   - SkinnedMeshRenderer：烘焙为静态 Mesh（T-Pose），LocalMatrix 为单位矩阵（顶点已在根空间）
        /// </summary>
        private static List<NpcMeshPart> ExtractMeshParts(GameObject prefab)
        {
            var result = new List<NpcMeshPart>();

            // 预制体根节点的世界矩阵逆矩阵，用于将子节点世界矩阵转换为相对根节点的本地矩阵
            Matrix4x4 rootInverse = prefab.transform.localToWorldMatrix.inverse;

            // ── 1. 提取 MeshFilter + MeshRenderer（装备/武器/盾牌等静态部件）──
            var filters = prefab.GetComponentsInChildren<MeshFilter>(false); // false = 只取激活的
            foreach (var mf in filters)
            {
                if (mf.sharedMesh == null) continue;

                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mr.sharedMaterial == null) continue;

                // 计算该部件相对于预制体根节点的本地变换矩阵
                Matrix4x4 localMatrix = rootInverse * mf.transform.localToWorldMatrix;

                result.Add(new NpcMeshPart
                {
                    PartName    = mf.gameObject.name,
                    Mesh        = mf.sharedMesh,
                    Material    = mr.sharedMaterial,
                    LocalMatrix = localMatrix
                });
            }

            // ── 2. 提取 SkinnedMeshRenderer（身体/蒙皮部件）──────────────────
            var skinned = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(false); // 只取激活的
            foreach (var smr in skinned)
            {
                if (smr.sharedMesh == null) continue;
                if (smr.sharedMaterial == null) continue;

                // 烘焙当前姿势（T-Pose）为静态 Mesh，顶点在 smr 节点本地空间
                var bakedMesh = new Mesh();
                bakedMesh.name = smr.gameObject.name + "_Baked";
                smr.BakeMesh(bakedMesh);

                // LocalMatrix = 该节点相对于预制体根节点的变换（与 MeshFilter 处理方式一致）
                Matrix4x4 skinnedLocalMatrix = rootInverse * smr.transform.localToWorldMatrix;
                result.Add(new NpcMeshPart
                {
                    PartName    = smr.gameObject.name,
                    Mesh        = bakedMesh,
                    Material    = smr.sharedMaterial,
                    LocalMatrix = skinnedLocalMatrix
                });
            }

            return result;
        }

        /// <summary>
        /// 检查现有 Bundle 是否与提取的部件列表一致（避免不必要的写入）
        /// </summary>
        private static bool IsBundleUpToDate(NpcMeshBundle bundle, List<NpcMeshPart> newParts)
        {
            if (bundle.Parts == null || bundle.Parts.Count != newParts.Count)
                return false;

            for (int i = 0; i < newParts.Count; i++)
            {
                if (bundle.Parts[i].Mesh     != newParts[i].Mesh)     return false;
                if (bundle.Parts[i].Material != newParts[i].Material) return false;
            }

            return true;
        }

        /// <summary>
        /// 删除 Resources/Prefabs/ 中不再被配置引用的旧 Bundle
        /// </summary>
        private static int CleanObsoleteBundles(HashSet<string> activeMeshNames)
        {
            int count = 0;
            var guids = AssetDatabase.FindAssets("t:NpcMeshBundle", new[] { DstDir });

            foreach (var guid in guids)
            {
                string path   = AssetDatabase.GUIDToAssetPath(guid);
                var    bundle = AssetDatabase.LoadAssetAtPath<NpcMeshBundle>(path);
                if (bundle == null) continue;

                if (!activeMeshNames.Contains(bundle.PrefabName))
                {
                    Debug.Log($"[NpcMeshSync] 清理过期 Bundle: {path}（{bundle.PrefabName} 不在配置中）");
                    AssetDatabase.DeleteAsset(path);
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 确保 AssetDatabase 中的目录存在
        /// </summary>
        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            int lastSlash = path.LastIndexOf('/');
            string parent = path.Substring(0, lastSlash);
            string folder = path.Substring(lastSlash + 1);
            EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }

        // ── JSON 数据模型（与 NPCSpawner 保持一致）────────────────────

        [Serializable]
        private class NPCDataWrapper
        {
            public List<NPCDataItem> NPCDataConf;
        }

        [Serializable]
        private class NPCDataItem
        {
            public string NPCID;
            public string NPCName;
            public string NPCPosition;
            public string NPCMesh;
            public string SceneName;
        }

    }
}
