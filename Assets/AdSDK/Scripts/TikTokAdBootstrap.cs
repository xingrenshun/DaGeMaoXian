using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 抖音广告初始化器（自举 + 补配置）。
///
/// 职责只有两个：
///   ① 场景/预制体里【已经挂了】AdManager → 尊重它的配置，只补齐它空着的字段（尤其是 AppId）；
///   ② 场景里确实没有 AdManager（例如直接进 Gameplay / CoinShop）→ 建一个并写入下面的兜底配置。
///
/// ⚠⚠ 2026-09-22 重要修正：必须是 AfterSceneLoad，绝不能是 BeforeSceneLoad！
///
///   本项目原来用的是 BeforeSceneLoad，结果真机上一直报
///     The AdUnitId is invalid（errorCode 1002）
///   排查半天广告位，其实根因在这里：
///
///     main.unity 场景里本来就挂着一个 AdManager，而且广告位 ID 填得好好的；
///     但 Init() 跑在场景加载【之前】，抢先占住了 AdManager.Instance；
///     场景里那个实例随后 Awake() 时发现 Instance 已存在，按单例写法
///     Destroy 掉了自己 —— 于是真正生效的变成了这里创建的那个【空配置】实例
///     （AppId 空、两个列表都空），最终回退到 AdManager.cs 里写死的、不属于本 App 的
///     默认广告位，抖音自然回 The AdUnitId is invalid。
///
///   口诀：**场景里挂了 AdManager 就绝不能用 BeforeSceneLoad 自举**，
///   否则「在 Inspector 里配好了却不生效」。
///
/// 编辑器下不会创建 AdManager，走 AdsManager 里的本地发奖分支，方便调试。
/// </summary>
public static class TikTokAdBootstrap
{
    // ================== 兜底配置 ==================
    // 仅在「场景里没有 AdManager」时使用；场景配了就以场景为准。
    /// <summary>抖音小游戏 AppId（必须与 StarkBuilderSetting.asset 的 _appId 一致）</summary>
    public const string AppId = "tt8474f2c91a0fb1c002";

    /// <summary>激励视频广告位 ID 列表（兜底）</summary>
    public static readonly string[] VideoAdIds =
    {
        "1s1dq6mujcl21i3b0o",
    };

    /// <summary>插屏广告位 ID 列表（兜底）</summary>
    public static readonly string[] InterAdIds =
    {
        "1p205m3edko373l8g6",
    };
    // ============================================

    // AfterSceneLoad：此时场景对象的 Awake 已执行（Instance 已就位），
    // 但 Start 尚未执行 —— 所以在这里补 AppId / 广告位列表，
    // 一定会早于 AdManager.Start 创建广告平台。绝不能改成 BeforeSceneLoad。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
#if UNITY_EDITOR
        Debug.Log("[广告] 编辑器环境：不创建抖音 AdManager，走本地模拟发奖");
#else
        // ① 场景里已有 AdManager —— 以它为准，只补空字段，绝不覆盖已填的值
        if (AdManager.Instance != null)
        {
            AdManager cur = AdManager.Instance;
            int patched = 0;

            if (string.IsNullOrEmpty(cur.AppId))
            {
                cur.AppId = AppId;
                patched++;
            }

            if (cur.videoAdIdList == null || cur.videoAdIdList.Count == 0)
            {
                cur.videoAdIdList = new List<string>(VideoAdIds);
                patched++;
            }

            if (cur.interAdIdList == null || cur.interAdIdList.Count == 0)
            {
                cur.interAdIdList = new List<string>(InterAdIds);
                patched++;
            }

            if (patched > 0)
                Debug.Log($"[广告] 场景中的 AdManager 有 {patched} 项为空，已用 TikTokAdBootstrap 的兜底值补齐（已填的值保持不变）");
            else
                Debug.Log("[广告] 使用场景中 AdManager 的配置（AppId 与广告位 ID 均已填好）");

            return;
        }

        // ② 场景里没有 AdManager —— 自建一个，写入兜底配置
        var go = new GameObject("[AdManager]");
        UnityEngine.Object.DontDestroyOnLoad(go);

        var mgr = go.AddComponent<AdManager>();
        mgr.adPlatformType = AdPlatformType.TickTok;
        mgr.AppId = AppId;

        if (VideoAdIds.Length > 0)
            mgr.videoAdIdList.AddRange(VideoAdIds);
        if (InterAdIds.Length > 0)
            mgr.interAdIdList.AddRange(InterAdIds);

        Debug.Log($"[广告] 场景内未找到 AdManager，已自建并写入兜底配置，AppId={AppId}");
#endif
    }
}
