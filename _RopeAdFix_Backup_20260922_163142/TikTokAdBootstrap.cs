using UnityEngine;

/// <summary>
/// 抖音广告初始化器。
///
/// 在第一个场景加载之前自动创建 AdManager 实例并写入配置，
/// 这样 Gameplay / CoinShop 两个场景都不用在编辑器里手工挂 AdManager。
///
/// 只需要填下面三个常量：
///   AppId       —— 抖音小游戏 AppId（必须以 "07" 结尾，AdManager 里会校验）
///   VideoAdIds  —— 激励视频广告位 ID（可配多个，运行时随机取一个）
///   InterAdIds  —— 插屏广告位 ID（可配多个，运行时随机取一个）
///
/// 编辑器下不会创建 AdManager，走 AdsManager 里的本地发奖分支，方便调试。
/// </summary>
public static class TikTokAdBootstrap
{
    // ================== 需要填写 ==================
    /// <summary>抖音小游戏 AppId</summary>
    public const string AppId = "";

    /// <summary>激励视频广告位 ID 列表</summary>
    public static readonly string[] VideoAdIds =
    {
        // "xxxxxxxxxxxxxxxxxx",
    };

    /// <summary>插屏广告位 ID 列表</summary>
    public static readonly string[] InterAdIds =
    {
        // "xxxxxxxxxxxxxxxxxx",
    };
    // ============================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
#if UNITY_EDITOR
        Debug.Log("[广告] 编辑器环境：不创建抖音 AdManager，走本地模拟发奖");
#else
        if (AdManager.Instance != null)
            return;

        var go = new GameObject("[AdManager]");
        UnityEngine.Object.DontDestroyOnLoad(go);

        var mgr = go.AddComponent<AdManager>();
        mgr.adPlatformType = AdPlatformType.TickTok;
        mgr.AppId = AppId;

        if (VideoAdIds.Length > 0)
            mgr.videoAdIdList.AddRange(VideoAdIds);
        if (InterAdIds.Length > 0)
            mgr.interAdIdList.AddRange(InterAdIds);

        Debug.Log($"[广告] 抖音 AdManager 已创建，AppId={(string.IsNullOrEmpty(AppId) ? "<空，需填写>" : AppId)}");
#endif
    }
}
