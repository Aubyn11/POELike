using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using POELike.Game;

namespace POELike.Editor
{
    /// <summary>
    /// 怪物 Mesh 同步工具
    /// 菜单：POELike → Monster → 同步 Monster Mesh 到 Resources
    ///
    /// 功能：
    ///   1. 读取 Assets/Cfg/MonstDataConf.pb，提取所有 MonsterMesh 字段（去重）
    ///   2. 从 TT_RTS 预制体目录找到对应预制体
    ///   3. 提取预制体中所有激活的 MeshFilter + MeshRenderer / SkinnedMeshRenderer
    ///   4. 生成轻量的 NpcMeshBundle（ScriptableObject）存入 Resources/Monsters/
    ///      只保留 Mesh + Material 引用，不含动画/碰撞体等冗余数据
    ///   5. 删除 Resources/Monsters/ 中不再被配置引用的旧 Bundle（自动清理）
    /// </summary>
    public static class MonsterMeshSync
    {
        // ── 路径常量 ──────────────────────────────────────────────────
        private const string ConfigPath   = "Assets/Cfg/MonstDataConf.pb";
        private const string SrcPrefabDir = "Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/prefabs";
        private const string DstDir       = "Assets/Resources/Monsters";

        // ── 菜单入口 ──────────────────────────────────────────────────

        [MenuItem("POELike/Monster/同步 Monster Mesh 到 Resources（运行前执行）")]
        public static void SyncMonsterMeshToResources()
        {
            // ── 1. 读取配置，提取 MonsterMesh 字段 ───────────────────
            var meshNames = ReadMonsterMeshNames();
            if (meshNames == null || meshNames.Count == 0)
            {
                EditorUtility.DisplayDialog("Monster Mesh 同步", "MonstDataConf.pb 中没有找到任何 MonsterMesh 配置。", "确定");
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
                    Debug.LogError($"[MonsterMeshSync] 提取 {meshName} 失败: {e}");
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

            EditorUtility.DisplayDialog("Monster Mesh 同步", msg, "确定");
            Debug.Log($"[MonsterMeshSync] {msg}");
        }

        // ── 核心逻辑 ──────────────────────────────────────────────────

        /// <summary>
        /// 从 MonstDataConf.pb 读取所有不重复的 MonsterMesh 名称
        /// </summary>
        private static HashSet<string> ReadMonsterMeshNames()
        {
            if (!File.Exists(ConfigPath))
            {
                Debug.LogError($"[MonsterMeshSync] 找不到配置文件: {ConfigPath}");
                return null;
            }

            string json = File.ReadAllText(ConfigPath);
            MonsterDataWrapper wrapper;
            try
            {
                wrapper = JsonUtility.FromJson<MonsterDataWrapper>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MonsterMeshSync] 解析 MonstDataConf.pb 失败: {e.Message}");
                return null;
            }

            var result = new HashSet<string>(StringComparer.Ordinal);
            if (wrapper?.MonstDataConf == null) return result;

            foreach (var monster in wrapper.MonstDataConf)
            {
                if (!string.IsNullOrEmpty(monster.MonsterMesh))
                    result.Add(monster.MonsterMesh.Trim());
            }

            Debug.Log($"[MonsterMeshSync] 从配置中读取到 {result.Count} 种 Monster Mesh: {string.Join(", ", result)}");
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
                Debug.LogWarning($"[MonsterMeshSync] 预制体 [{meshName}] 中没有找到任何激活的渲染部件，跳过。");
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

            Debug.Log($"[MonsterMeshSync] [{meshName}] {(isNew ? "新建" : "更新")} Bundle，共 {parts.Count} 个部件 → {dstPath}");
            return true;
        }

        /// <summary>
        /// 从预制体中提取所有激活的渲染部件：
        ///   - MeshFilter + MeshRenderer：直接使用 sharedMesh，LocalMatrix 为相对根节点的变换
        ///   - SkinnedMeshRenderer：烘焙为静态 Mesh（T-Pose），LocalMatrix 为相对根节点的变换
        /// </summary>
        private static List<NpcMeshPart> ExtractMeshParts(GameObject prefab)
        {
            var result = new List<NpcMeshPart>();

            // 预制体根节点的世界矩阵逆矩阵，用于将子节点世界矩阵转换为相对根节点的本地矩阵
            Matrix4x4 rootInverse = prefab.transform.localToWorldMatrix.inverse;

            // ── 1. 提取 MeshFilter + MeshRenderer（装备/武器/盾牌等静态部件）──
            var filters = prefab.GetComponentsInChildren<MeshFilter>(false);
            foreach (var mf in filters)
            {
                if (mf.sharedMesh == null) continue;

                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mr.sharedMaterial == null) continue;

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
            var skinned = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(false);
            foreach (var smr in skinned)
            {
                if (smr.sharedMesh == null) continue;
                if (smr.sharedMaterial == null) continue;

                var bakedMesh = new Mesh();
                bakedMesh.name = smr.gameObject.name + "_Baked";
                smr.BakeMesh(bakedMesh);

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
        /// 删除 Resources/Monsters/ 中不再被配置引用的旧 Bundle
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
                    Debug.Log($"[MonsterMeshSync] 清理过期 Bundle: {path}（{bundle.PrefabName} 不在配置中）");
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

        // ── JSON 数据模型（与 MonstDataConf.pb 结构对应）─────────────

        [Serializable]
        private class MonsterDataWrapper
        {
            public List<MonsterDataItem> MonstDataConf;
        }

        [Serializable]
        private class MonsterDataItem
        {
            public string MonsterID;
            public string MonsterMesh;
            public string MonsterHp;
        }
    }
}
