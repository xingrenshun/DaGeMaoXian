// MainSceneSetup.cs
// 让 main 场景（标题页）的「开始游戏」按钮真正生效，并把它登记为首个 Build 场景。
//
// 做四件事（全部幂等，可以反复执行）：
//   1) Build Settings 顺序调整为 main(0) -> Gameplay(1) -> CoinShop(2)，main 成为启动场景；
//   2) 给 /Canvas 挂上 MainMenuController；
//   3) 把 paly 按钮的 OnClick 指向 MainMenuController.StartGame（Inspector 可见）；
//   4) 按钮文字换成含汉字的中文字体；背景类 Image 关掉 Raycast Target，
//      保证整屏只有这个按钮能接收点击。
//
// 用法：切回 Unity 会自动执行一次；也可手动执行菜单 Tools/打个毛线/设置开始场景并接好「开始游戏」按钮。
//
// 注意：本工具用 Unity 自己的 API 改场景（不手写 YAML），避免上次缩进写错导致组件被重置的问题。

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
    public static class MainSceneSetup
    {
        private const string SceneMain     = "Assets/MyAssets/Scene/main.unity";
        private const string SceneGameplay = "Assets/MyAssets/Scene/Gameplay.unity";
        private const string SceneCoinShop = "Assets/MyAssets/Scene/CoinShop.unity";
        private const string FontPath      = "Assets/AdSDK/Font/DroidSansFallback.ttf";
        private const string MenuRoot      = "Tools/打个毛线/";

        private const string ButtonName    = "paly";
        private const string MethodName    = "StartGame";

        /// <summary>把 Raycast Target 关掉的纯装饰节点（层级路径）。</summary>
        private static readonly string[] DecorPaths =
        {
            "/Canvas/Panel",
            "/Canvas/Panel/bg",
        };

        private static bool _busy;
        private static bool _autoRan;

        // ------------------------------------------------------------ 入口

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.delayCall += AutoRun;
        }

        private static void AutoRun()
        {
            if (_autoRan) return;
            _autoRan = true;
            Run(false);
        }

        [MenuItem(MenuRoot + "设置开始场景并接好「开始游戏」按钮", false, 20)]
        private static void MenuRun()
        {
            int n = Run(true);
            EditorUtility.DisplayDialog("main 场景设置",
                n > 0
                    ? "已完成，共修改 " + n + " 处。\n场景与 Build Settings 均已保存。"
                    : "已经是最新状态，无需修改。",
                "好");
        }

        [MenuItem(MenuRoot + "检查 main 场景状态", false, 21)]
        private static void MenuReport()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("== Build Settings ==");
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
                sb.AppendLine("  [" + i + "] " + (scenes[i].enabled ? "ON " : "off") + "  " + scenes[i].path);

            sb.AppendLine();
            sb.AppendLine("== main 场景 ==");
            Scene sc = SceneManager.GetSceneByPath(SceneMain);
            if (!sc.IsValid() || !sc.isLoaded)
            {
                sb.AppendLine("  (未打开，无法检查按钮绑定)");
            }
            else
            {
                Button btn = FindStartButton(sc);
                if (btn == null)
                {
                    sb.AppendLine("  找不到按钮 " + ButtonName);
                }
                else
                {
                    sb.AppendLine("  按钮: " + PathOf(btn.transform));
                    sb.AppendLine("  持久绑定数: " + btn.onClick.GetPersistentEventCount());
                    MainMenuController ctrl = FindController(sc);
                    sb.AppendLine("  MainMenuController: " + (ctrl == null ? "未挂载" : "已挂载"));
                    sb.AppendLine("  按钮字体: " + (btn.GetComponentInChildren<Text>() == null
                        ? "(无 Text)" : btn.GetComponentInChildren<Text>().font.name));
                }
            }

            Debug.Log("[主菜单] 状态检查\n" + sb);
            EditorUtility.DisplayDialog("main 场景状态", sb.ToString(), "好");
        }

        // ------------------------------------------------------------ 实现

        private static int Run(bool verbose)
        {
            if (_busy) return 0;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return 0;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return 0;

            _busy = true;
            int n = 0;
            try
            {
                n += SetupBuildSettings();
                n += SetupMainScene();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[主菜单] 设置失败: " + e);
            }
            finally
            {
                _busy = false;
            }

            if (n > 0)
                Debug.Log("[主菜单] main 场景设置完成，共修改 " + n + " 处"
                          + (verbose ? "（手动）" : "（自动）"));
            else if (verbose)
                Debug.Log("[主菜单] main 场景已是最新状态");

            return n;
        }

        // ------------------------------------------------------------ Build Settings

        private static int SetupBuildSettings()
        {
            string[] want = { SceneMain, SceneGameplay, SceneCoinShop };

            List<EditorBuildSettingsScene> current =
                new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            List<EditorBuildSettingsScene> target = new List<EditorBuildSettingsScene>();

            // 目标场景按固定顺序排在最前
            for (int i = 0; i < want.Length; i++)
            {
                if (!File.Exists(want[i]))
                {
                    Debug.LogWarning("[主菜单] 场景不存在，跳过: " + want[i]);
                    continue;
                }

                EditorBuildSettingsScene found = null;
                for (int j = 0; j < current.Count; j++)
                {
                    if (string.Equals(current[j].path, want[i], System.StringComparison.OrdinalIgnoreCase))
                    {
                        found = current[j];
                        break;
                    }
                }

                if (found == null)
                    found = new EditorBuildSettingsScene(want[i], true);
                else
                    found.enabled = true;

                target.Add(found);
            }

            // 其余场景原样保留在后面
            for (int j = 0; j < current.Count; j++)
            {
                bool already = false;
                for (int i = 0; i < want.Length; i++)
                {
                    if (string.Equals(current[j].path, want[i], System.StringComparison.OrdinalIgnoreCase))
                        already = true;
                }
                if (!already) target.Add(current[j]);
            }

            // 和现状一致就不动，避免无意义的 ProjectSettings 写入
            if (Same(target, current)) return 0;

            EditorBuildSettings.scenes = target.ToArray();
            return 1;
        }

        private static bool Same(List<EditorBuildSettingsScene> a, List<EditorBuildSettingsScene> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i].path, b[i].path, System.StringComparison.OrdinalIgnoreCase)) return false;
                if (a[i].enabled != b[i].enabled) return false;
            }
            return true;
        }

        // ------------------------------------------------------------ main 场景

        private static int SetupMainScene()
        {
            if (!File.Exists(SceneMain))
            {
                Debug.LogWarning("[主菜单] 找不到场景: " + SceneMain);
                return 0;
            }

            Scene scene = SceneManager.GetSceneByPath(SceneMain);
            bool openedByUs = false;

            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(SceneMain, OpenSceneMode.Additive);
                openedByUs = true;
            }

            int changed = 0;
            try
            {
                // 每步独立容错：任何一步抛异常都不会影响其它步骤，也不会阻止后面的保存
                changed += TryStep("挂载 MainMenuController", SetupController, scene);
                changed += TryStep("绑定按钮 OnClick", SetupButton, scene);
                changed += TryStep("修正字体", SetupFonts, scene);
                changed += TryStep("关闭装饰 Raycast Target", SetupRaycast, scene);
            }
            finally
            {
                // 只要有任何改动就要落盘，否则改动会在关场景时丢掉
                if (changed > 0)
                {
                    try
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError("[主菜单] 保存 main 场景失败: " + e);
                    }
                }

                if (openedByUs)
                {
                    try { EditorSceneManager.CloseScene(scene, true); }
                    catch (System.Exception e) { Debug.LogWarning("[主菜单] 关闭 main 场景失败: " + e); }
                }
            }

            return changed;
        }

        /// <summary>执行一步设置，失败只记日志不中断。</summary>
        private static int TryStep(string label, System.Func<Scene, int> step, Scene scene)
        {
            try
            {
                return step(scene);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[主菜单] 步骤「" + label + "」失败: " + e);
                return 0;
            }
        }

        /// <summary>给 /Canvas 挂 MainMenuController。</summary>
        private static int SetupController(Scene scene)
        {
            GameObject canvas = FindByName(scene, "Canvas");
            if (canvas == null)
            {
                Debug.LogWarning("[主菜单] main 场景里找不到 Canvas");
                return 0;
            }

            if (canvas.GetComponent<MainMenuController>() != null) return 0;

            MainMenuController ctrl = canvas.AddComponent<MainMenuController>();
            EditorUtility.SetDirty(ctrl);
            Debug.Log("[主菜单] 已在 " + PathOf(canvas.transform) + " 上挂载 MainMenuController");
            return 1;
        }

        /// <summary>把按钮 OnClick 指向 MainMenuController.StartGame。</summary>
        private static int SetupButton(Scene scene)
        {
            Button button = FindStartButton(scene);
            if (button == null)
            {
                Debug.LogWarning("[主菜单] 找不到开始游戏按钮（期望节点名 " + ButtonName + "）");
                return 0;
            }

            MainMenuController ctrl = FindController(scene);
            if (ctrl == null)
            {
                Debug.LogWarning("[主菜单] MainMenuController 未挂载，跳过按钮绑定");
                return 0;
            }

            if (!WireOnClick(button, ctrl)) return 0;

            Debug.Log("[主菜单] 已把 " + PathOf(button.transform) + " 的 OnClick 指向 " + MethodName);
            return 1;
        }

        private static bool WireOnClick(Button button, MainMenuController target)
        {
            SerializedObject so = new SerializedObject(button);
            SerializedProperty calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            if (calls == null)
            {
                Debug.LogWarning("[主菜单] 读不到按钮的 OnClick 属性");
                return false;
            }

            // 已经是正确的唯一绑定就直接跳过
            if (calls.arraySize == 1)
            {
                SerializedProperty only = calls.GetArrayElementAtIndex(0);
                if (only.FindPropertyRelative("m_Target").objectReferenceValue == target &&
                    only.FindPropertyRelative("m_MethodName").stringValue == MethodName)
                    return false;
            }

            calls.ClearArray();
            calls.InsertArrayElementAtIndex(0);

            SerializedProperty call = calls.GetArrayElementAtIndex(0);
            call.FindPropertyRelative("m_Target").objectReferenceValue = target;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue =
                typeof(MainMenuController).FullName + ", " + typeof(MainMenuController).Assembly.GetName().Name;
            call.FindPropertyRelative("m_MethodName").stringValue = MethodName;
            call.FindPropertyRelative("m_Mode").enumValueIndex = 1;       // PersistentListenerMode.Void
            call.FindPropertyRelative("m_CallState").enumValueIndex = 2;  // UnityEventCallState.RuntimeOnly

            SerializedProperty args = call.FindPropertyRelative("m_Arguments");
            args.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = null;
            args.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue = string.Empty;
            args.FindPropertyRelative("m_IntArgument").intValue = 0;
            args.FindPropertyRelative("m_FloatArgument").floatValue = 0f;
            args.FindPropertyRelative("m_StringArgument").stringValue = string.Empty;
            args.FindPropertyRelative("m_BoolArgument").boolValue = false;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(button);
            return true;
        }

        /// <summary>把场景里的中文文本换成含汉字的中文字体（原来的 Arial 没有汉字，会显示空白）。</summary>
        private static int SetupFonts(Scene scene)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
            {
                Debug.LogError("[主菜单] 找不到字体资源: " + FontPath);
                return 0;
            }

            int changed = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Text[] texts = roots[r].GetComponentsInChildren<Text>(true);
                for (int t = 0; t < texts.Length; t++)
                {
                    if (texts[t].font == font) continue;

                    string before = texts[t].font == null ? "(空)" : texts[t].font.name;
                    texts[t].font = font;
                    EditorUtility.SetDirty(texts[t]);
                    changed++;
                    Debug.Log("[主菜单] 字体 " + PathOf(texts[t].transform) + " : " + before + " -> " + font.name);
                }
            }
            return changed;
        }

        /// <summary>装饰性 Image 不再接收点击，保证只有按钮能触发。</summary>
        private static int SetupRaycast(Scene scene)
        {
            int changed = 0;
            GameObject[] roots = scene.GetRootGameObjects();

            for (int r = 0; r < roots.Length; r++)
            {
                Image[] images = roots[r].GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    string path = PathOf(images[i].transform);
                    if (!IsDecor(path)) continue;
                    if (!images[i].raycastTarget) continue;

                    images[i].raycastTarget = false;
                    EditorUtility.SetDirty(images[i]);
                    changed++;
                    Debug.Log("[主菜单] 关闭 Raycast Target: " + path);
                }
            }
            return changed;
        }

        private static bool IsDecor(string path)
        {
            for (int i = 0; i < DecorPaths.Length; i++)
                if (DecorPaths[i] == path) return true;
            return false;
        }

        // ------------------------------------------------------------ 工具方法

        private static MainMenuController FindController(Scene scene)
        {
            GameObject canvas = FindByName(scene, "Canvas");
            if (canvas != null)
            {
                MainMenuController c = canvas.GetComponent<MainMenuController>();
                if (c != null) return c;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                MainMenuController c = roots[r].GetComponentInChildren<MainMenuController>(true);
                if (c != null) return c;
            }
            return null;
        }

        private static Button FindStartButton(Scene scene)
        {
            Button fallback = null;
            int count = 0;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Button[] buttons = roots[r].GetComponentsInChildren<Button>(true);
                for (int b = 0; b < buttons.Length; b++)
                {
                    count++;
                    fallback = buttons[b];
                    if (buttons[b].name == ButtonName) return buttons[b];
                }
            }

            return count == 1 ? fallback : null;
        }

        private static GameObject FindByName(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                if (roots[r].name == name) return roots[r];

                Transform[] all = roots[r].GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                    if (all[i].name == name) return all[i].gameObject;
            }
            return null;
        }

        private static string PathOf(Transform t)
        {
            string s = t.name;
            Transform p = t.parent;
            while (p != null)
            {
                s = p.name + "/" + s;
                p = p.parent;
            }
            return "/" + s;
        }
    }
}
