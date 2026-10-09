using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 修复 TTSDK (ByteGame) 在 Unity 6000.x 上的 WebGL / 抖音小游戏构建阻塞。
///
/// 【本工具负责的三项设置】
///
/// ① StarkBuilderSetting.asset 的 enableEmscriptenGlx : 1 -> 0
///    现象：Exception occurred during BuildPlayer: Failed to activate emscripten glx
///    调用栈：TTSDKBase.Tool.BuildPlatformWebGL.InitializeBeforeBuildPlayer
///            -> NativePluginManager.ProcessEmscriptenGlxLib(bool)
///    原因：TTSDK 6.9.4 只随包提供两版预编译 GLX 静态库
///            libemscriptenglx_2021.a      （Unity 2021）
///            libemscriptenglx_3_1_39.a    （Unity 2022 / emscripten 3.1.39）
///          当前 Unity 6000.2.15f1 没有匹配的库 → 工具把候选全部 Disable 后
///          不降级、直接抛异常，构建中途中断（连包体都产不出来）。
///          GLX 是可选优化，关掉不影响玩法与上架。
///
/// ② StarkBuilderSetting.asset 的 isWebGL2 : 0 -> 1
///    原因：Unity 6 起只支持 WebGL 2.0（WebGL 1 已被移除）。该字段会被 TTSDK
///          用来覆盖 PlayerSettings 的 Graphics API，设 0 会把它压回 WebGL 1.0。
///    注意：本工程色彩空间是 Gamma，所以不会出现 Linear 下那条
///          "Incompatible color space" 提示，别因此以为跟 WebGL2 无关。
///
/// ③ PlayerSettings.WebGL.template : (空) -> "APPLICATION:Default"
///    现象：Exception: No WebGL template selected! Select a template in player settings.
///    调用栈：UnityEditor.WebGL.WebGlBuildPostprocessor.UpdateHTMLTemplatePath()
///            -> PostProcess -> BuildPipeline.BuildPlayer
///    说明：这条报错出现时【三个场景其实已经打包完成】（日志里能看到
///          "Level 0/1/2 ... Total compressed size ..."），只是最后生成 index.html
///          时找不到模板，属于最后一步失败，前面白跑。
///
/// 【为什么走脚本而不是直接改文件】
///   编辑器开着时，从外部改 .meta / ProjectSettings.asset 会被内存里的旧版本写回
///   （实测 ProjectSettings.asset 与贴图 .meta 都出现过）。所以统一走 Unity 自己的
///   API + SerializedObject，由编辑器落盘。
///
/// 【⚠ 2026-09-22 追加：为什么"改一次就好了"不成立】
///   上一轮把 enableEmscriptenGlx 改成 0 之后确实构建成功过一次，但第二天又复发了。
///   Unity 日志给出了铁证（Editor.log）：
///
///      16:13:05  Start importing Assets/Editor/StarkBuilderSetting.asset ...
///      16:13:05  <color=green>build begin</color>, workspace: ...\tt-minigame
///      16:13:06  Disable EmscriptenGLX importer path: ...libemscriptenglx_3_1_39.a
///      16:13:06  Disable EmscriptenGLX importer path: ...libemscriptenglx.a
///      16:13:06  Disable EmscriptenGLX importer path: ...libemscriptenglx_2021.a
///      16:13:06  EmscriptenGLX plugin not supported for current Unity version
///      16:13:06  Exception: Failed to activate emscripten glx
///
///   【结论】不是"设置没改对"，而是 **TTSDK 工具窗口每次点构建时都会先把它自己内存里的
///   StarkBuilderSettings 重新落盘一遍**（就是那条 Start importing），而它的 UI 里
///   "启用 Emscripten GLX" 的勾选状态会把 enableEmscriptenGlx 带成 1 —— 正好发生在我们
///   的校验之后、构建读取之前。所以这个值必须【在每次写入的瞬间】被压回去，改一次文件没用。
///
///   【对策】RopeTTSDKSettingsGuard（AssetPostprocessor）监听该 asset 的导入事件，
///   一旦发现它被写回 enableEmscriptenGlx=1，立刻改【内存实例】的字段。
///   TTSDK.Tool.StarkSDKToolWindow:BuildByNewTool(StarkBuilderSettings) 拿的正是这个
///   内存实例（见日志调用栈），所以改完它读到的就是 0，不会再进 GLX 分支。
///   注意这里【刻意不调 AssetDatabase.SaveAssets】—— 在导入回调里保存会造成递归，
///   落盘交给 delayCall 延后处理。另有 EditorApplication.update 秒级守卫兜底。
///
/// 【兜底】
///   RopeBuildPreflight 会在 BuildPipeline.BuildPlayer 真正开始前再确认一次模板，
///   即使设置被外部改回，也不会再出现 "No WebGL template selected!"。
/// </summary>
[InitializeOnLoad]
public static class RopeTTSDKBuildFix
{
    private const string AssetPath = "Assets/Editor/StarkBuilderSetting.asset";
    private const string Tag = "[TTSDK构建修复] ";

    // Unity 6 上正确的取值
    private const int WantEmscriptenGlx = 0; // 关闭 GLX
    private const int WantWebGL2 = 1;        // WebGL 2.0

    /// <summary>Unity 6 应使用的 WebGL 模板（工程内无 Assets/WebGLTemplates，用内置 Default）。</summary>
    public const string WantTemplate = "APPLICATION:Default";

    /// <summary>秒级守卫的间隔，避免每帧反射。</summary>
    private const double WatchInterval = 1.0;

    private static double s_nextWatch;
    private static bool s_savePending;

    static RopeTTSDKBuildFix()
    {
        EditorApplication.delayCall += AutoFix;
        EditorApplication.update += Watch;
    }

    /// <summary>
    /// 秒级守卫：只要内存实例里的两个开关不是 Unity 6 需要的值，就压回去。
    /// 这样即使 TTSDK 工具窗口不经过落盘、直接改内存后立即构建，也拦得住。
    /// </summary>
    private static void Watch()
    {
        // 构建进行中不干预（GLX 的判定发生在构建最开头，此刻再改已无意义，
        // 而且在打包过程中动设置可能引起 Unity 内部状态不一致）
        if (BuildPipeline.isBuildingPlayer)
            return;

        double now = EditorApplication.timeSinceStartup;
        if (now < s_nextWatch)
            return;
        s_nextWatch = now + WatchInterval;

        try
        {
            int n = ForceMemoryInstance();
            if (n > 0)
            {
                Debug.LogWarning(Tag + "拦截到 TTSDK 工具窗口把 enableEmscriptenGlx 改回了 1 —— "
                    + "Unity 6 下没有任何可用的 GLX 预编译库，开着它构建必报 "
                    + "\"Failed to activate emscripten glx\"。已即时改回 0，请直接点构建。");
            }
        }
        catch (Exception)
        {
            // 工具窗口关闭 / 类型未加载时静默，不影响使用
        }
    }

    /// <summary>
    /// 只改【内存实例】的字段，不落盘（落盘交给 delayCall）。
    /// 返回实际改动的项数。
    ///
    /// 为什么不用 SerializedObject：这里会被 AssetPostprocessor 在导入过程中调用，
    /// SerializedObject + ApplyModifiedProperties 在导入中容易触发重入；反射改字段最轻。
    ///
    /// markDirty 传 false 的情况：由 AssetModificationProcessor.OnWillSaveAssets 调用时，
    /// 此刻正在保存流程里，再 SetDirty 会造成"保存完又脏了"的循环。
    /// </summary>
    public static int ForceMemoryInstance(bool markDirty = true)
    {
        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(AssetPath);
        if (asset == null)
            return 0;

        int changed = 0;
        if (ForceField(asset, "enableEmscriptenGlx", WantEmscriptenGlx))
            changed++;
        if (ForceField(asset, "isWebGL2", WantWebGL2))
            changed++;

        if (changed > 0 && markDirty)
        {
            EditorUtility.SetDirty(asset);
            ScheduleSave();
        }

        return changed;
    }

    /// <summary>延后落盘，让文件也停在正确的值上；带去重，避免连续触发。</summary>
    private static void ScheduleSave()
    {
        if (s_savePending)
            return;
        s_savePending = true;

        EditorApplication.delayCall += () =>
        {
            s_savePending = false;

            // 打包期间不允许保存资源，跳过即可：内存值已经正确，
            // 文件里的值会在下一次域重载时由 AutoFix 补正。
            if (BuildPipeline.isBuildingPlayer)
                return;

            try
            {
                AssetDatabase.SaveAssets();
            }
            catch (Exception)
            {
                // 构建过程中保存失败无所谓，内存值已经是正确的
            }
        };
    }

    /// <summary>用反射改字段，兼容 bool / int 两种序列化类型，并向上遍历基类。</summary>
    private static bool ForceField(object target, string name, int want)
    {
        Type t = target.GetType();
        FieldInfo f = null;
        while (t != null && f == null)
        {
            f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            t = t.BaseType;
        }

        if (f == null)
            return false;

        if (f.FieldType == typeof(bool))
        {
            bool v = want != 0;
            if ((bool)f.GetValue(target) == v)
                return false;
            f.SetValue(target, v);
            return true;
        }

        if (f.FieldType == typeof(int))
        {
            if ((int)f.GetValue(target) == want)
                return false;
            f.SetValue(target, want);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 读取 StarkBuilderSetting.asset 上的字符串字段（用于核对 _appId）。
    /// 用反射是因为该类型的程序集每次版本升级都可能改名，硬引用会编译不过。
    /// </summary>
    public static string ReadAppIdFromSettings()
    {
        try
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(AssetPath);
            if (asset == null)
                return null;

            Type t = asset.GetType();
            FieldInfo f = null;
            while (t != null && f == null)
            {
                f = t.GetField("_appId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                t = t.BaseType;
            }

            if (f == null)
                return null;

            return f.GetValue(asset) as string;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void AutoFix()
    {
        int changed = 0;
        try
        {
            changed = ApplyFix();
        }
        catch (Exception e)
        {
            Debug.LogWarning(Tag + "自动修复出错（不影响其它功能）：" + e.Message);
        }

        if (changed > 0)
        {
            Debug.Log(Tag + "已修正 " + changed + " 项设置（自动）。现在可以回到 TTSDK 工具窗口重新点击构建。");
        }
        else
        {
            Debug.Log(Tag + "构建设置已是正确值，无需改动（自动）。");
        }
    }

    [MenuItem("Tools/打个毛线/修复 TTSDK 构建设置（Unity 6）")]
    public static void FixByMenu()
    {
        int changed = 0;
        try
        {
            changed = ApplyFix();
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "修复失败：" + e.Message);
            return;
        }

        if (changed > 0)
            Debug.Log(Tag + "已修正 " + changed + " 项设置，可以重新构建。");
        else
            Debug.Log(Tag + "构建设置已是正确值，无需改动。");
    }

    [MenuItem("Tools/打个毛线/检查 TTSDK 构建设置")]
    public static void CheckByMenu()
    {
        Report();
    }

    /// <summary>把三项设置写成 Unity 6 需要的值，返回实际改动的项数。</summary>
    private static int ApplyFix()
    {
        int changed = 0;
        changed += ApplyBuilderAsset();
        changed += ApplyWebGLTemplate();

        if (changed > 0)
        {
            try
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogWarning(Tag + "保存设置时出错：" + e.Message);
            }
        }

        return changed;
    }

    /// <summary>修正 StarkBuilderSetting.asset 里的两个开关。</summary>
    private static int ApplyBuilderAsset()
    {
        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(AssetPath);
        if (asset == null)
        {
            Debug.LogWarning(Tag + "找不到 " + AssetPath + "，请先打开一次 TTSDK 工具窗口让它生成该文件。");
            return 0;
        }

        int changed = 0;

        try
        {
            SerializedObject so = new SerializedObject(asset);

            SerializedProperty glx = SafeFind(so, "enableEmscriptenGlx");
            SerializedProperty webgl2 = SafeFind(so, "isWebGL2");

            if (glx == null)
                Debug.LogWarning(Tag + "未找到字段 enableEmscriptenGlx，TTSDK 版本可能已变化，请人工确认。");
            else if (SetValue(glx, WantEmscriptenGlx))
                changed++;

            if (webgl2 == null)
                Debug.LogWarning(Tag + "未找到字段 isWebGL2，TTSDK 版本可能已变化，请人工确认。");
            else if (SetValue(webgl2, WantWebGL2))
                changed++;

            if (changed > 0)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "写入 " + AssetPath + " 时出错：" + e.Message);
        }

        return changed;
    }

    /// <summary>确保 PlayerSettings 里选了 WebGL 模板（空值会让构建最后一步失败）。</summary>
    private static int ApplyWebGLTemplate()
    {
        string cur;
        try
        {
            cur = PlayerSettings.WebGL.template;
        }
        catch (Exception e)
        {
            Debug.LogWarning(Tag + "读取 WebGL 模板失败：" + e.Message);
            return 0;
        }

        if (string.Equals(cur, WantTemplate, StringComparison.Ordinal))
            return 0;

        try
        {
            PlayerSettings.WebGL.template = WantTemplate;
            Debug.Log(Tag + "WebGL 模板 : \"" + (cur == null ? "" : cur) + "\"  ->  \"" + WantTemplate + "\"");
            return 1;
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "设置 WebGL 模板失败（请手工到 Player Settings ▸ WebGL ▸ Resolution and Presentation 里选 Default）：" + e.Message);
            return 0;
        }
    }

    private static SerializedProperty SafeFind(SerializedObject so, string name)
    {
        try
        {
            return so.FindProperty(name);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>兼容 bool / int 两种序列化类型。</summary>
    private static bool SetValue(SerializedProperty p, int want)
    {
        if (p == null)
            return false;

        if (p.propertyType == SerializedPropertyType.Boolean)
        {
            bool v = want != 0;
            if (p.boolValue == v)
                return false;
            p.boolValue = v;
            return true;
        }

        if (p.propertyType == SerializedPropertyType.Integer)
        {
            if (p.intValue == want)
                return false;
            p.intValue = want;
            return true;
        }

        Debug.LogWarning(Tag + p.name + " 的类型是 " + p.propertyType + "，未做处理。");
        return false;
    }

    /// <summary>打印当前状态，便于人工核对。</summary>
    public static void Report()
    {
        Debug.Log(Tag + "===== 构建设置检查 =====");

        // ① StarkBuilderSetting.asset
        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(AssetPath);
        if (asset == null)
        {
            Debug.LogWarning(Tag + "  找不到 " + AssetPath + "。");
        }
        else
        {
            try
            {
                SerializedObject so = new SerializedObject(asset);
                Dump(so, "enableEmscriptenGlx", WantEmscriptenGlx, "应为 0（Unity 6 没有匹配的 GLX 库）");
                Dump(so, "isWebGL2", WantWebGL2, "应为 1（Unity 6 只支持 WebGL 2.0）");
                Dump(so, "isOldBuildFormat", -1, "旧格式打包，保持 0");
                Dump(so, "scriptingBackend", -1, "");
                Dump(so, "stripEngineCode", -1, "");
            }
            catch (Exception e)
            {
                Debug.LogWarning(Tag + "  读取 " + AssetPath + " 失败：" + e.Message);
            }
        }

        // ② WebGL 模板
        try
        {
            string cur = PlayerSettings.WebGL.template;
            bool ok = string.Equals(cur, WantTemplate, StringComparison.Ordinal);
            Debug.Log(Tag + "  PlayerSettings.WebGL.template = \"" + (cur == null ? "" : cur) + "\""
                      + (ok ? "  [OK]" : "  [!! 需要修，应为 \"" + WantTemplate + "\"]"));
        }
        catch (Exception e)
        {
            Debug.LogWarning(Tag + "  读取 WebGL 模板失败：" + e.Message);
        }
    }

    private static void Dump(SerializedObject so, string name, int expect, string note)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null)
        {
            Debug.Log(Tag + "  " + name + " : (无此字段)");
            return;
        }

        string val;
        int cur;
        if (p.propertyType == SerializedPropertyType.Boolean)
        {
            cur = p.boolValue ? 1 : 0;
            val = p.boolValue ? "1" : "0";
        }
        else if (p.propertyType == SerializedPropertyType.Integer)
        {
            cur = p.intValue;
            val = p.intValue.ToString();
        }
        else
        {
            Debug.Log(Tag + "  " + name + " : (类型 " + p.propertyType + "，跳过)");
            return;
        }

        bool ok = (expect < 0) || (cur == expect);
        string flag = (expect < 0) ? "" : (ok ? "  [OK]" : "  [!! 需要修]");
        string suffix = string.IsNullOrEmpty(note) ? "" : "  (" + note + ")";
        Debug.Log(Tag + "  " + name + " = " + val + flag + suffix);
    }
}

/// <summary>
/// 构建前兜底：在 BuildPipeline.BuildPlayer 真正开始之前，再确认一次 WebGL 模板。
///
/// 为什么需要它：构建失败的报错点在 WebGlBuildPostprocessor.PostProcess（BuildPlayer 内部），
/// 那时再改设置已经来不及。这个回调会在 BuildPlayer 启动后、真正打包前执行，
/// 所以即使 ProjectSettings.asset 被外部脚本或编辑器写回成空值，也能在这里补上，
/// 不会再出现 "No WebGL template selected!" 导致整轮构建白跑。
/// </summary>
public class RopeBuildPreflight : IPreprocessBuildWithReport
{
    private const string Tag = "[TTSDK构建修复] ";

    public int callbackOrder { get { return 0; } }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report == null)
            return;

        if (report.summary.platform != BuildTarget.WebGL)
            return;

        try
        {
            string cur = PlayerSettings.WebGL.template;
            if (!string.Equals(cur, RopeTTSDKBuildFix.WantTemplate, StringComparison.Ordinal))
            {
                PlayerSettings.WebGL.template = RopeTTSDKBuildFix.WantTemplate;
                Debug.Log(Tag + "构建前已把 WebGL 模板由 \"" + (cur == null ? "" : cur)
                          + "\" 修正为 \"" + RopeTTSDKBuildFix.WantTemplate + "\"");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning(Tag + "构建前修正 WebGL 模板失败：" + e.Message);
        }

        // ── Enable Exceptions：None -> Full Without Stacktrace ───────────────
        // 为什么必须开（2026-09-22「点下一关卡死」事故）：
        //   None 时 WebGL 里 **任何托管异常都无法被捕获，会直接 abort 整个 WASM**，
        //   于是抖音开发者工具只报「Uncaught exception from main loop / Halting program」，
        //   Unity 侧连一条可读的报错都没有，整局游戏直接死掉、无法恢复。
        //   代码里的 try/catch 也一样失效（DISABLE_EXCEPTION_CATCHING），
        //   连 DOTween 的 safeMode 都救不回来。
        //   改成 FullWithoutStacktrace 后，未捕获异常只会被记录下来并继续跑，
        //   以后再遇到空引用/索引越界，是「一条红字」而不是「整个游戏没了」。
        // 代价：包体增加（约几百 KB 量级）、性能略降；当前包体 16MB 左右，余量充足。
        //
        // ⚠ 必须放在这里（构建前钩子）而不是只在 ProjectSettings.asset 里改：
        //   TTSDK 的 SetupWebGLBuildSettings 会在构建时覆盖 PlayerSettings，
        //   而这个回调的时机晚于它，最终生效的是我们这里的值。
        try
        {
            if (PlayerSettings.WebGL.exceptionSupport != WebGLExceptionSupport.FullWithoutStacktrace)
            {
                Debug.Log(Tag + "构建前已把 Enable Exceptions 由 \""
                          + PlayerSettings.WebGL.exceptionSupport + "\" 修正为 \"FullWithoutStacktrace\"");
                PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithoutStacktrace;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning(Tag + "构建前修正 Enable Exceptions 失败：" + e.Message);
        }

        // ── AppId 一致性核对：构建用的 appid 必须与广告 SDK 用的 appid 是同一个 ──
        // 为什么（2026-09-22 发现）：构建 appid 来自 StarkBuilderSetting.asset 的 _appId，
        //   广告 SDK 用的 appid 来自 TikTokAdBootstrap.AppId（因为 main 场景 AdManager 的
        //   AppId 字段是空的，由自举在 AfterSceneLoad 补上）。
        //   两者是【两套独立的配置入口】，很容易改了一个忘另一个：
        //   构建 appid 决定产物归属哪个小游戏，广告 appid 决定埋点/上报记到哪个小游戏名下。
        //   不一致时广告位可能被判为「不属于当前应用」→ The AdUnitId is invalid (1002)。
        // 所以这里在构建前对一次，不一致就醒目地报出来，避免又跑一轮才发现。
        try
        {
            string buildAppId = RopeTTSDKBuildFix.ReadAppIdFromSettings();
            string adAppId = TikTokAdBootstrap.AppId;

            if (!string.IsNullOrEmpty(buildAppId) && !string.Equals(buildAppId, adAppId, StringComparison.Ordinal))
            {
                Debug.LogError(Tag + "AppId 不一致！"
                               + "\n    StarkBuilderSetting._appId（构建/上传归属） = " + buildAppId
                               + "\n    TikTokAdBootstrap.AppId（广告埋点/上报）  = " + adAppId
                               + "\n    → 请把两者改成同一个 tt 开头的 appid，否则广告可能被判为不属于本应用而返回 1002。");
            }
            else
            {
                Debug.Log(Tag + "AppId 核对通过：" + buildAppId);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning(Tag + "构建前核对 AppId 失败：" + e.Message);
        }
    }
}

/// <summary>
/// 关键守卫（2026-09-22 加）：TTSDK 工具窗口点「构建」时，会先把自己内存里的
/// StarkBuilderSettings 重新序列化落盘 —— 就是 Editor.log 里紧跟 build begin 之前那条
/// "Start importing Assets/Editor/StarkBuilderSetting.asset"。这一步会把
/// enableEmscriptenGlx 带成 1，卡在「我们的校验之后、构建读取之前」，
/// 所以光靠「改一次文件」永远会被它覆盖，第二天再构建就又挂了。
///
/// 这里监听该 asset 的导入事件，在它刚落盘的那一刻就把【内存实例】改回 0。
/// 因为 TTSDK.Tool.StarkSDKToolWindow:BuildByNewTool(StarkBuilderSettings) 直接把
/// 这个内存实例传进构建流程，改完它读到的就是 0，不会再走进 GLX 分支。
/// </summary>
public class RopeTTSDKSettingsGuard : AssetPostprocessor
{
    private const string Target = "Assets/Editor/StarkBuilderSetting.asset";
    private const string Tag = "[TTSDK构建修复] ";

    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        if (importedAssets == null || importedAssets.Length == 0)
            return;

        for (int i = 0; i < importedAssets.Length; i++)
        {
            if (!string.Equals(importedAssets[i], Target, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                int n = RopeTTSDKBuildFix.ForceMemoryInstance();
                if (n > 0)
                {
                    Debug.LogWarning(Tag + "构建前拦截：StarkBuilderSetting.asset 刚被写回 "
                        + "enableEmscriptenGlx=1，已即时改回 0。"
                        + "（Unity 6 没有任何匹配的 GLX 预编译库，开着它构建必报 Failed to activate emscripten glx）");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(Tag + "拦截 StarkBuilderSetting.asset 写回时出错：" + e.Message);
            }

            return;
        }
    }
}

/// <summary>
/// 第三层守卫（保存【前】拦截）：AssetModificationProcessor.OnWillSaveAssets 在
/// 资源真正落到磁盘之前被调用。在这里把 enableEmscriptenGlx 改成 0，
/// 那么 TTSDK 工具窗口这次保存写进文件的就是 0，而不是它内存里的 1。
///
/// 和 RopeTTSDKSettingsGuard（保存后拦截）一前一后，把"值被带回 1"这件事从两头堵死。
/// ⚠ 必须是 private static string[] 的签名，且【原样返回 paths】——
///   返回 null 或空数组会让保存被静默阻止。
/// </summary>
public class RopeTTSDKSettingsSaveGuard : AssetModificationProcessor
{
    private const string Target = "Assets/Editor/StarkBuilderSetting.asset";
    private const string Tag = "[TTSDK构建修复] ";

    private static string[] OnWillSaveAssets(string[] paths)
    {
        if (paths == null)
            return paths;

        for (int i = 0; i < paths.Length; i++)
        {
            if (!string.Equals(paths[i], Target, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                // markDirty:false —— 正在保存流程中，不能再 SetDirty（会变成"保存完又是脏的"死循环）
                int n = RopeTTSDKBuildFix.ForceMemoryInstance(false);
                if (n > 0)
                {
                    Debug.LogWarning(Tag + "保存前拦截：StarkBuilderSetting.asset 里 "
                        + "enableEmscriptenGlx 被带回 1，已在落盘前改回 0。");
                }
            }
            catch (Exception)
            {
                // 保存流程中出错不能抛，否则会中断保存
            }

            break;
        }

        return paths;
    }
}
