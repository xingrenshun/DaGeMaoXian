// RopeUITextRestore.cs
// 修复 legacy UI Text 组件被重置的问题（字体丢失 / 文本被清空 / 字号与对齐回默认）。
//
// 背景：2026-09-19 的中文化脚本把 .unity 里的 m_Font 行写错了缩进层级，
//       Unity 反序列化失败 -> 16 个 Text 组件整体回默认值 -> 编辑器保存后落盘。
//       本工具按「组件 fileID（GlobalObjectId）+ 层级路径」双键还原原始
//       字号 / 样式 / 对齐 / 最大字号，字体统一指到 DroidSansFallback（含汉字），
//       文本按中文化方案写回。
//
// 用法：切回 Unity 会自动修复一次；也可手动执行菜单 Tools/打个毛线/修复场景文本。

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RopeTools
{
    public static class RopeUITextRestore
    {
        private const string FontPath = "Assets/AdSDK/Font/DroidSansFallback.ttf";
        private const string SceneGameplay = "Assets/MyAssets/Scene/Gameplay.unity";
        private const string SceneCoinShop = "Assets/MyAssets/Scene/CoinShop.unity";
        private const string MenuRoot = "Tools/打个毛线/";

        private static bool _busy;

        private struct Rec
        {
            public readonly ulong Id;
            public readonly string Path;
            public readonly string Text;
            public readonly int Size;
            public readonly int Style;
            public readonly int Align;
            public readonly int Max;

            public Rec(ulong id, string path, string text, int size, int style, int align, int max)
            {
                Id = id; Path = path; Text = text;
                Size = size; Style = style; Align = align; Max = max;
            }
        }

        // fileID 取自 _RopeScene_Backup_20260919_110241\Gameplay.unity（未损坏的原始版本）
        private static readonly Rec[] Table =
        {
            //                                                                                中文                字号 样式 对齐 最大字号   原标题
            new Rec(5341988264442179055UL, "/Canvas/CommingSoonPanel/Button/Text (3)",                "主页",            72, 0, 4, 121), // HOME
            new Rec(5295914411729253747UL, "/Canvas/CommingSoonPanel/Text",                           "更多关卡 敬请期待", 73, 1, 4, 75),  // More Levels Comming Soon
            new Rec(4233765803677117673UL, "/Canvas/LevelText",                                       "第 00 关",         17, 1, 3, 27),  // Level 00
            new Rec(7068533881067550083UL, "/Canvas/RetryPanel/Button/Text (3)",                      "重试",            72, 0, 4, 121), // RETRY
            new Rec(4603506151775950046UL, "/Canvas/RetryPanel/Text",                                 "关卡失败",         73, 1, 4, 75),  // Level Failed
            new Rec(5590601144214825332UL, "/Canvas/SpinWheelPanel/CoinLessSpinBtn/Text (Legacy)",    "抽奖 -100",       43, 0, 4, 51),  // SPIN      -100
            new Rec(8977661797393382177UL, "/Canvas/SpinWheelPanel/SpinBtn/Text (Legacy)",            "抽奖",            43, 0, 4, 51),  // SPIN
            new Rec(324487474684018960UL,  "/Canvas/SpinWheelPanel/TimerText",                        "可以抽奖啦！",      48, 1, 4, 77),  // New Text
            new Rec(8310379454275338687UL, "/Canvas/SpinWheelPanel/WinCoinsPanel/Ok/Text (Legacy)",   "关闭",            36, 0, 4, 49),  // CLOSE
            new Rec(8182085956957252599UL, "/Canvas/SpinWheelPanel/WinCoinsPanel/Text (Legacy) (1)",  "恭喜中奖",         49, 0, 4, 53),  // CONGRATULATIONS
            new Rec(6571732621131782390UL, "/Canvas/StartPanel/CoinsShop/Text (2)",                   "商店",            41, 0, 4, 41),  // SHOP
            new Rec(7783238999612817862UL, "/Canvas/StartPanel/SkipLevelBtn/Text (2)",                "跳过关卡",         27, 1, 4, 41),  // SKIP LEVEL
            new Rec(1176399782136498693UL, "/Canvas/StartPanel/SpinWheelBtn/Text (Legacy)",           "抽奖",            32, 1, 4, 40),  // SPIN
            new Rec(163506243936370339UL,  "/Canvas/StartPanel/Text (1)",                             "点击开始",         89, 0, 4, 94),  // TAP TO PLAY
            new Rec(7068832169627714408UL, "/Canvas/WinPanel/Button/Text (3)",                        "下一关",           72, 0, 4, 121), // NEXT
            new Rec(8869913791207564165UL, "/Canvas/WinPanel/Text",                                   "关卡完成",         73, 1, 4, 75),  // Level Complete
        };

        private static Dictionary<ulong, Rec> _byId;
        private static Dictionary<string, Rec> _byPath;

        private static void EnsureMaps()
        {
            if (_byId != null) return;
            _byId = new Dictionary<ulong, Rec>(Table.Length);
            _byPath = new Dictionary<string, Rec>(Table.Length);
            for (int i = 0; i < Table.Length; i++)
            {
                _byId[Table[i].Id] = Table[i];
                _byPath[Table[i].Path] = Table[i];
            }
        }

        // ------------------------------------------------------------ 入口

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.delayCall += AutoRun;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.path == SceneGameplay || scene.path == SceneCoinShop)
                Run(false, scene.path);
        }

        private static void AutoRun()
        {
            Run(false, null);
        }

        [MenuItem(MenuRoot + "修复场景文本（中文化）", false, 10)]
        private static void MenuFix()
        {
            int n = Run(true, null);
            EditorUtility.DisplayDialog("修复场景文本",
                n > 0 ? "已恢复 " + n + " 个 Text 组件，场景已保存。" : "没有发现需要修复的 Text 组件。",
                "好");
        }

        [MenuItem(MenuRoot + "检查场景文本状态", false, 11)]
        private static void MenuReport()
        {
            StringBuilder sb = new StringBuilder();
            string[] list = { SceneGameplay, SceneCoinShop };
            for (int i = 0; i < list.Length; i++)
            {
                Scene sc = SceneManager.GetSceneByPath(list[i]);
                if (!sc.IsValid() || !sc.isLoaded)
                {
                    sb.AppendLine(Path.GetFileName(list[i]) + " : 未打开");
                    continue;
                }
                int total = 0, broken = 0;
                GameObject[] roots = sc.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    Text[] ts = roots[r].GetComponentsInChildren<Text>(true);
                    for (int t = 0; t < ts.Length; t++)
                    {
                        total++;
                        if (ts[t].font == null || string.IsNullOrEmpty(ts[t].text)) broken++;
                    }
                }
                sb.AppendLine(Path.GetFileName(list[i]) + " : Text=" + total + "，异常=" + broken);
            }
            Debug.Log("[文本修复] 状态检查\n" + sb);
            EditorUtility.DisplayDialog("场景文本状态", sb.ToString(), "好");
        }

        // ------------------------------------------------------------ 实现

        private static int Run(bool force, string onlyScene)
        {
            if (_busy) return 0;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return 0;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return 0;

            _busy = true;
            int total = 0;
            try
            {
                if (!string.IsNullOrEmpty(onlyScene))
                {
                    if (onlyScene == SceneGameplay || onlyScene == SceneCoinShop)
                        total += RepairScene(onlyScene, false);
                    return total;
                }

                total += RepairScene(SceneGameplay, true);
                total += RepairScene(SceneCoinShop, true);

                if (total > 0)
                    Debug.Log("[文本修复] 完成，共处理 " + total + " 个 Text 组件"
                              + (force ? "（手动）" : "（自动）"));
                else if (force)
                    Debug.Log("[文本修复] 未发现需要修复的 Text 组件");
            }
            finally
            {
                _busy = false;
            }
            return total;
        }

        private static int RepairScene(string scenePath, bool openIfClosed)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool opened = false;

            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (!openIfClosed) return 0;
                string abs = Path.Combine(Directory.GetCurrentDirectory(),
                                          scenePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(abs))
                {
                    Debug.LogWarning("[文本修复] 场景不存在: " + scenePath);
                    return 0;
                }
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                opened = true;
            }

            int n = FixInScene(scene);
            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (opened) EditorSceneManager.CloseScene(scene, true);
            return n;
        }

        private static int FixInScene(Scene scene)
        {
            EnsureMaps();

            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
            {
                Debug.LogError("[文本修复] 找不到字体资源: " + FontPath);
                return 0;
            }

            int fixedCount = 0;
            List<string> leftovers = new List<string>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Text t in root.GetComponentsInChildren<Text>(true))
                {
                    bool fontBroken = t.font == null;
                    bool textBroken = string.IsNullOrEmpty(t.text);
                    if (!fontBroken && !textBroken) continue;

                    ulong id = GlobalObjectId.GetGlobalObjectIdSlow(t).targetObjectId;
                    string path = PathOf(t.transform);

                    Rec rec;
                    bool matched = _byId.TryGetValue(id, out rec) || _byPath.TryGetValue(path, out rec);

                    if (matched)
                    {
                        t.font = font;
                        t.text = rec.Text;
                        t.fontSize = rec.Size;
                        t.fontStyle = (FontStyle)rec.Style;
                        t.alignment = (TextAnchor)rec.Align;
                        t.resizeTextMaxSize = rec.Max;
                    }
                    else
                    {
                        // 不在中文化表里（例如 CoinShop 只丢了字体）-> 只补字体，不动文案
                        t.font = font;
                    }

                    EditorUtility.SetDirty(t);
                    fixedCount++;
                    Debug.Log("[文本修复] " + scene.name + "  " + path + "  ->  \"" + t.text + "\"");

                    if (t.font == null || string.IsNullOrEmpty(t.text))
                        leftovers.Add(path);
                }
            }

            if (leftovers.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < leftovers.Count; i++) sb.AppendLine("  " + leftovers[i]);
                Debug.LogWarning("[文本修复] 以下 Text 仍未恢复，需要人工确认：\n" + sb);
            }

            return fixedCount;
        }

        private static string PathOf(Transform t)
        {
            StringBuilder sb = new StringBuilder(t.name);
            Transform p = t.parent;
            while (p != null)
            {
                sb.Insert(0, '/').Insert(0, p.name);
                p = p.parent;
            }
            return "/" + sb;
        }
    }
}
