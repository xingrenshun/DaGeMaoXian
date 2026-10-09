using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 中文字体（TMP）自动生成工具 —— 打个毛线 Rope
///
/// 为什么需要：工程里唯一用 TMP 渲染的文本是 Toast（"金币不足" 之类），
/// 它的字体是拉丁字体、没有汉字，不生成中文 TMP 字体就会显示空白。
///
/// 作用：
///   1. 用 AdSDK 自带的中文字体源生成 TMP 字体资源（含工程内用到的全部汉字）
///   2. 把它挂到 TMP 全局回退表 + 所有业务字体的回退表上，保证中文能正常显示
///
/// 打开 Unity 后会自动执行一次（用 EditorPrefs 记录，不会重复执行）。
/// 也可以手动执行菜单：Tools / 打个毛线 / 生成中文TMP字体。
/// </summary>
[InitializeOnLoad]
public static class ChineseFontBuilder
{
    /// <summary>主字体源：覆盖最全的汉字（AdSDK 自带）</summary>
    private const string PrimaryFontPath = "Assets/AdSDK/Font/DroidSansFallback.ttf";

    /// <summary>备选字体源的搜索目录</summary>
    private const string FontSearchDir = "Assets/AdSDK/Font";

    /// <summary>生成的 TMP 字体资源位置</summary>
    private const string OutputDir = "Assets/MyAssets/Font";
    private const string AssetName = "Chinese SDF";

    private const int SamplingPointSize = 48;
    private const int AtlasPadding = 4;
    private const int AtlasSize = 1024;

    /// <summary>执行标记：换版本号可以强制重新生成一次</summary>
    private const string DoneKey = "Rope.ChineseFontBuilder.v1";

    static ChineseFontBuilder()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }

    private static void AutoRunOnce()
    {
        if (EditorPrefs.GetBool(DoneKey, false))
        {
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoRunOnce;
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        try
        {
            if (BuildChineseFont())
            {
                EditorPrefs.SetBool(DoneKey, true);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[中文字体] 自动生成失败：" + e);
        }
    }

    [MenuItem("Tools/打个毛线/生成中文TMP字体")]
    public static void BuildChineseFontMenu()
    {
        BuildChineseFont();
        EditorPrefs.SetBool(DoneKey, true);
    }

    [MenuItem("Tools/打个毛线/强制重新生成中文TMP字体")]
    public static void RebuildChineseFontMenu()
    {
        EditorPrefs.SetBool(DoneKey, false);
        BuildChineseFont();
    }

    /// <summary>生成/更新中文字体资源并挂好回退关系。成功返回 true。</summary>
    public static bool BuildChineseFont()
    {
        Font source = FindSourceFont();

        if (source == null)
        {
            Debug.LogError("[中文字体] 找不到中文字体源文件：" + PrimaryFontPath);
            return false;
        }

        if (!Directory.Exists(OutputDir))
        {
            Directory.CreateDirectory(OutputDir);
            AssetDatabase.Refresh();
        }

        string savePath = OutputDir + "/" + AssetName + ".asset";
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(savePath);

        if (fontAsset == null)
        {
            fontAsset = TMP_FontAsset.CreateFontAsset(
                source,
                SamplingPointSize,
                AtlasPadding,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                AtlasSize,
                AtlasSize,
                AtlasPopulationMode.Dynamic);

            fontAsset.name = AssetName;
            AssetDatabase.CreateAsset(fontAsset, savePath);
        }

        // 收集工程里实际用到的字符并烘焙进图集
        string characters = CollectUsedCharacters();
        string missing = string.Empty;
        bool added = false;

        try
        {
            added = fontAsset.TryAddCharacters(characters, out missing);
        }
        catch (Exception e)
        {
            // TMP 已知问题：字体资源刚 CreateAsset、图集还没落盘时，
            // 字符超过第一页容量会在 SetupNewAtlasTexture 里空引用。
            // 先把资源落盘，再重试一次即可正常写入第二页。
            Debug.LogWarning("[中文字体] 首次烘焙触发 TMP 图集异常，落盘后重试：" + e.GetType().Name);
            RegisterSubAssets(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            added = fontAsset.TryAddCharacters(characters, out missing);
        }

        if (!string.IsNullOrEmpty(missing))
        {
            Debug.LogWarning("[中文字体] 以下字符在字体源中不存在：" + missing);
        }

        RegisterSubAssets(fontAsset);

        int hooked = HookGlobalFallback(fontAsset);
        hooked += HookAllFontAssets(fontAsset);

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(string.Format(
            "[中文字体] 生成完成：{0}\n  字符数 {1}，烘焙 {2}\n  已挂到 {3} 处回退表\n  字形数 {4}，图集 {5} 张",
            savePath,
            characters.Length,
            added ? "成功" : "失败",
            hooked,
            fontAsset.glyphTable != null ? fontAsset.glyphTable.Count : 0,
            fontAsset.atlasTextures != null ? fontAsset.atlasTextures.Length : 0));

        return true;
    }

    private static Font FindSourceFont()
    {
        Font f = AssetDatabase.LoadAssetAtPath<Font>(PrimaryFontPath);
        if (f != null)
        {
            return f;
        }

        string[] guids = AssetDatabase.FindAssets("t:Font", new[] { FontSearchDir });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("DroidSansFallback"))
            {
                f = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (f != null)
                {
                    return f;
                }
            }
        }

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            f = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (f != null)
            {
                return f;
            }
        }

        return null;
    }

    /// <summary>把材质与图集纹理登记成子资源，否则重启编辑器后引用会丢失</summary>
    private static void RegisterSubAssets(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null)
        {
            return;
        }

        if (fontAsset.material != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(fontAsset.material)))
        {
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        if (fontAsset.atlasTextures == null)
        {
            return;
        }

        foreach (Texture2D tex in fontAsset.atlasTextures)
        {
            if (tex != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(tex)))
            {
                AssetDatabase.AddObjectToAsset(tex, fontAsset);
            }
        }
    }

    private static int HookGlobalFallback(TMP_FontAsset fontAsset)
    {
        try
        {
            TMP_Settings settings = TMP_Settings.instance;
            if (settings == null)
            {
                return 0;
            }

            System.Reflection.FieldInfo field = typeof(TMP_Settings).GetField(
                "m_fallbackFontAssets",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field == null)
            {
                return 0;
            }

            List<TMP_FontAsset> list = field.GetValue(settings) as List<TMP_FontAsset>;
            if (list == null)
            {
                list = new List<TMP_FontAsset>();
            }

            if (!list.Contains(fontAsset))
            {
                list.Add(fontAsset);
                field.SetValue(settings, list);
                EditorUtility.SetDirty(settings);
                return 1;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[中文字体] 写入 TMP 全局回退表失败：" + e.Message);
        }

        return 0;
    }

    private static int HookAllFontAssets(TMP_FontAsset chinese)
    {
        int count = 0;
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (path.Contains("/TextMesh Pro/") || path.Contains("Examples & Extras"))
            {
                continue;
            }

            TMP_FontAsset fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fa == null || fa == chinese)
            {
                continue;
            }

            List<TMP_FontAsset> table = fa.fallbackFontAssetTable;
            if (table == null)
            {
                table = new List<TMP_FontAsset>();
            }

            if (!table.Contains(chinese))
            {
                table.Add(chinese);
                fa.fallbackFontAssetTable = table;
                EditorUtility.SetDirty(fa);
                count++;
            }
        }

        return count;
    }

    /// <summary>扫描工程里用到的全部汉字、中文标点</summary>
    private static string CollectUsedCharacters()
    {
        HashSet<char> chars = new HashSet<char>();

        string[] files = Directory.GetFiles(Application.dataPath, "*.*", SearchOption.AllDirectories);

        foreach (string file in files)
        {
            if (!(file.EndsWith(".cs") || file.EndsWith(".unity") || file.EndsWith(".prefab") ||
                  file.EndsWith(".asset") || file.EndsWith(".json") || file.EndsWith(".txt")))
            {
                continue;
            }

            // Editor 下的代码不进包、也不会在游戏里渲染，跳过可以避免把源码里的
            // 转义字面量（如 '\u9fff'）当成真汉字收进来
            if (file.Contains("TextMesh Pro") || file.Contains("ByteGame") ||
                file.Contains("GoogleMobileAds") || file.Contains("MobileDependencyResolver") ||
                file.Contains("ExternalDependencyManager") ||
                file.Contains("/Editor/") || file.Contains("\\Editor\\"))
            {
                continue;
            }

            string text;
            try
            {
                text = File.ReadAllText(file, Encoding.UTF8);
            }
            catch
            {
                continue;
            }

            foreach (char c in text)
            {
                if (IsChinese(c))
                {
                    chars.Add(c);
                }
            }

            // Unity 会把非 ASCII 字符写成 \uXXXX，这里补一次转义形式的解析
            foreach (char c in DecodeEscapes(text))
            {
                if (IsChinese(c))
                {
                    chars.Add(c);
                }
            }
        }

        // 常用字兜底，避免运行时出现缺字
        const string common =
            "关卡商店重试胜利双倍领取奖励每日时间设置支持关于其他游戏服务条款确定取消稍后名称已解锁" +
            "已选择广告是否你喜欢这款小游戏吗去给我们五星好评吧即表示同意用户协议与隐私政策" +
            "点击按钮等级收集物品总数下次倒计时体力不足加载中请再试排行榜返回确认获得分享侧边栏开始" +
            "暂停继续得分金币钻石免费立即购买成功失败错误提示网络连接关闭开启音量最高分数本次记录刷新新纪录";

        foreach (char c in common)
        {
            chars.Add(c);
        }

        char[] arr = new char[chars.Count];
        chars.CopyTo(arr);
        Array.Sort(arr);
        return new string(arr);
    }

    private static bool IsChinese(char c)
    {
        return (c >= '\u4e00' && c <= '\u9fff') ||
               (c >= '\u3400' && c <= '\u4dbf') ||
               (c >= '\u3000' && c <= '\u303f') ||
               (c >= '\uff00' && c <= '\uffef') ||
               (c >= '\u2018' && c <= '\u201f');
    }

    private static string DecodeEscapes(string text)
    {
        if (text.IndexOf('\\') < 0)
        {
            return string.Empty;
        }

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i + 5 < text.Length; i++)
        {
            if (text[i] != '\\' || text[i + 1] != 'u')
            {
                continue;
            }

            int code;
            if (int.TryParse(text.Substring(i + 2, 4), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out code))
            {
                sb.Append((char)code);
                i += 5;
            }
        }

        return sb.ToString();
    }
}
