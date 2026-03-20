using UnityEngine;
using UnityEditor;
using System.IO;

namespace POELike.Editor.UI
{
    /// <summary>
    /// 将 CharacterSelectPanel Prefab 复制到 Resources/UI/ 目录
    /// 菜单：POELike → UI → ③ 同步 Prefab 到 Resources
    /// </summary>
    public static class ResourcesPrefabSync
    {
        private const string SrcPath = "Assets/Prefabs/UI/CharacterSelectPanel.prefab";
        private const string DstDir  = "Assets/Resources/UI";
        private const string DstPath = "Assets/Resources/UI/CharacterSelectPanel.prefab";

        [MenuItem("POELike/UI/③ 同步 Prefab 到 Resources（运行前执行）")]
        public static void SyncToResources()
        {
            // 检查源文件
            if (!File.Exists(SrcPath))
            {
                EditorUtility.DisplayDialog("错误",
                    $"未找到源 Prefab：{SrcPath}\n请先执行「② 生成角色选择界面 Prefab」。",
                    "确定");
                return;
            }

            // 确保目标目录存在
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(DstDir))
                AssetDatabase.CreateFolder("Assets/Resources", "UI");

            // 复制（覆盖）
            AssetDatabase.CopyAsset(SrcPath, DstPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ResourcesPrefabSync] 已同步：{SrcPath} → {DstPath}");
            EditorUtility.DisplayDialog("完成",
                $"Prefab 已同步到 Resources/UI/\n\n现在可以运行游戏，启动时将自动加载角色选择面板。",
                "确定");
        }
    }
}
