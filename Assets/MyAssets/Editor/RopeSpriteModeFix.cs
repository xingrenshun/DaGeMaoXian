// RopeSpriteModeFix.cs
// 把误设成 Sprite Mode = Multiple(多精灵) 但一张都没切片的单张图，改回 Single(单张精灵)。
// 症状：Sprite Mode=Multiple 且 spriteSheet.sprites 为空 -> 该贴图不会生成任何 Sprite，
//       在 Image 的 Source Image 选择器里选不到、Resources.Load<Sprite> 也拿不到。
// 走 Unity 内部 API 修改（外部直接改 .meta 会被已打开的编辑器写回）。
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rope.EditorTools
{
    public static class RopeSpriteModeFix
    {
        // 工程内已确认"应是单张精灵"但被设成 Multiple 的贴图
        static readonly string[] Targets =
        {
            "Assets/AdSDK/Resources/按钮.png",
            "Assets/AdSDK/Resources/位图备份 2.png",
            "Assets/AdSDK/Resources/ima1.png",
        };

        const string DoneKey = "Rope.SpriteModeFix.v1";
        const string LogTag = "[精灵模式]";

        // 切回 Unity 重编译后自动跑一次
        static RopeSpriteModeFix()
        {
            // 延后到 AssetDatabase 就绪后执行
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetBool(DoneKey, false)) return;
                int n = Run(false);
                if (n >= 0) EditorPrefs.SetBool(DoneKey, true);
            };
        }

        [MenuItem("Tools/打个毛线/修复精灵模式（Multiple→Single）")]
        static void RunFromMenu()
        {
            int n = Run(true);
            EditorPrefs.SetBool(DoneKey, true);
            Debug.Log(LogTag + " 手动执行完成，共处理 " + n + " 个贴图。");
        }

        [MenuItem("Tools/打个毛线/检查精灵模式状态")]
        static void Check()
        {
            foreach (string path in Targets)
            {
                TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null)
                {
                    Debug.LogWarning(LogTag + " 找不到贴图：" + path);
                    continue;
                }
                int n = CountSprites(ti);
                Debug.Log(string.Format("{0} {1}  模式={2}  已切分精灵={3}",
                    LogTag, path, ti.spriteImportMode, n));
            }
        }

        static int Run(bool verbose)
        {
            int changed = 0;
            foreach (string path in Targets)
            {
                try
                {
                    TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti == null)
                    {
                        if (verbose) Debug.LogWarning(LogTag + " 找不到贴图：" + path);
                        continue;
                    }

                    bool isMultiple = ti.spriteImportMode == SpriteImportMode.Multiple;
                    int spriteCount = CountSprites(ti);

                    if (!isMultiple)
                    {
                        if (verbose)
                            Debug.Log(string.Format("{0} {1}  已经是 Single，跳过。", LogTag, path));
                        continue;
                    }

                    if (spriteCount > 0)
                    {
                        if (verbose)
                            Debug.LogWarning(string.Format(
                                "{0} {1} 是 Multiple 但已有 {2} 个切片，不动它（这是真的图集）。",
                                LogTag, path, spriteCount));
                        continue;
                    }

                    // 只改模式，其它设置（尺寸/压缩/最大尺寸）保持原样
                    ti.spriteImportMode = SpriteImportMode.Single;
                    ti.SaveAndReimport();

                    Debug.Log(string.Format("{0} {1}  Multiple -> Single 完成（原本 0 个切片，生不出精灵）。",
                        LogTag, path));
                    changed++;
                }
                catch (System.Exception e)
                {
                    // 单个失败不影响其它
                    Debug.LogError(string.Format("{0} 处理 {1} 失败：{2}", LogTag, path, e.Message));
                }
            }

            Debug.Log(string.Format("{0} 全部完成，共修改 {1} 个贴图{2}。",
                LogTag, changed, verbose ? "（手动）" : "（自动）"));
            return changed;
        }

        static int CountSprites(TextureImporter ti)
        {
            try
            {
                SpriteMetaData[] sheet = ti.spritesheet;
                return sheet == null ? 0 : sheet.Length;
            }
            catch
            {
                return 0;
            }
        }
    }
}
