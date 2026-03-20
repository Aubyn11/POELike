using UnityEngine;
using UnityEditor;
using TMPro;
using System.IO;

namespace POELike.Editor.UI
{
    /// <summary>
    /// 中文 TMP 字体资源创建工具
    /// 菜单：POELike → UI → ① 创建中文字体资源（先执行）
    /// </summary>
    public static class ChineseFontCreator
    {
        // 生成的 TMP 字体资源保存路径
        public const string FontAssetPath = "Assets/Fonts/ChineseFont SDF.asset";

        // 项目内字体文件路径（复制系统字体后的位置）
        private const string FontFilePath = "Assets/Fonts/ChineseFont.ttf";

        // 候选系统字体文件（按优先级，优先选 .ttf，避免 .ttc 兼容问题）
        private static readonly string[] CandidateFontFiles = new[]
        {
            @"C:\Windows\Fonts\simhei.ttf",    // 黑体
            @"C:\Windows\Fonts\simfang.ttf",   // 仿宋
            @"C:\Windows\Fonts\simkai.ttf",    // 楷体
            @"C:\Windows\Fonts\simsun.ttf",    // 宋体（部分系统有 .ttf 版）
            @"C:\Windows\Fonts\simsunb.ttf",   // 宋体 Bold
        };

        // ── 菜单入口 ──────────────────────────────────────────────────
        [MenuItem("POELike/UI/① 创建中文字体资源（先执行）")]
        public static void CreateChineseFontAsset()
        {
            var fontAsset = BuildFontAsset();
            if (fontAsset == null) return;

            Debug.Log($"[ChineseFontCreator] 中文字体资源已创建：{FontAssetPath}");
            EditorUtility.DisplayDialog("完成",
                $"中文字体资源已创建！\n路径：{FontAssetPath}\n\n现在可以执行「生成角色选择界面 Prefab」了。",
                "确定");
        }

        // ── 供 Builder 调用的静态方法 ─────────────────────────────────

        /// <summary>
        /// 获取中文 TMP 字体资源。
        /// 若资源已存在则直接加载；否则自动创建后返回。
        /// 若创建失败则返回 null（使用 TMP 默认字体）。
        /// </summary>
        public static TMP_FontAsset GetOrCreateChineseFontAsset()
        {
            // 1. 尝试加载已有资源
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
                return existing;

            // 2. 尝试自动创建
            return BuildFontAsset();
        }

        // ── 核心构建逻辑 ──────────────────────────────────────────────

        private static TMP_FontAsset BuildFontAsset()
        {
            EnsureDirectory("Assets/Fonts");

            // 删除旧资源，避免旧的损坏资源（Atlas Texture 未嵌入）被复用
            if (File.Exists(Path.GetFullPath(FontAssetPath)))
            {
                AssetDatabase.DeleteAsset(FontAssetPath);
                Debug.Log("[ChineseFontCreator] 已删除旧字体资源，重新生成。");
            }

            // Step 1：确保项目内有字体文件
            if (!EnsureFontFileInProject())
            {
                EditorUtility.DisplayDialog("错误",
                    "未找到可用的系统中文字体文件（simhei.ttf / simfang.ttf 等）。\n" +
                    "请手动将中文 .ttf 文件复制到 Assets/Fonts/ChineseFont.ttf 后重试。",
                    "确定");
                return null;
            }

            // Step 2：通过 AssetDatabase 加载 Font 对象（必须是已导入的资源）
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontFilePath);
            if (font == null)
            {
                Debug.LogError($"[ChineseFontCreator] 无法从 AssetDatabase 加载字体：{FontFilePath}");
                return null;
            }

            // Step 3：创建 TMP_FontAsset
            var fontAsset = TMP_FontAsset.CreateFontAsset(font);
            if (fontAsset == null)
            {
                Debug.LogError("[ChineseFontCreator] TMP_FontAsset.CreateFontAsset 失败，请检查字体文件是否有效。");
                return null;
            }

            fontAsset.name = "ChineseFont SDF";

            // Step 4：先保存主资源
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            // Step 5：将 Atlas Texture 和 Material 嵌入同一个 .asset 文件
            // 这是关键步骤：若不嵌入，运行时 Texture2D 会被 GC 回收，导致 MissingReferenceException
            if (fontAsset.atlasTextures != null)
            {
                foreach (var tex in fontAsset.atlasTextures)
                {
                    if (tex != null)
                    {
                        tex.name = "Atlas";
                        AssetDatabase.AddObjectToAsset(tex, FontAssetPath);
                    }
                }
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = "Atlas Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, FontAssetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        }

        /// <summary>
        /// 确保 Assets/Fonts/ChineseFont.ttf 存在（从系统字体目录复制）。
        /// 返回 true 表示文件已就绪。
        /// </summary>
        private static bool EnsureFontFileInProject()
        {
            // 已存在则直接返回
            if (File.Exists(Path.GetFullPath(FontFilePath)))
            {
                AssetDatabase.ImportAsset(FontFilePath, ImportAssetOptions.ForceUpdate);
                return true;
            }

            // 从系统字体目录复制
            foreach (var src in CandidateFontFiles)
            {
                if (!File.Exists(src)) continue;

                File.Copy(src, Path.GetFullPath(FontFilePath));
                AssetDatabase.ImportAsset(FontFilePath, ImportAssetOptions.ForceUpdate);
                Debug.Log($"[ChineseFontCreator] 已复制系统字体：{src} → {FontFilePath}");
                return true;
            }

            return false;
        }

        // ── 目录辅助 ──────────────────────────────────────────────────

        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts  = path.Split('/');
            var parent = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var full = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(full))
                    AssetDatabase.CreateFolder(parent, parts[i]);
                parent = full;
            }
        }
    }
}
