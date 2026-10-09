using System;
using UnityEngine;

/// <summary>
/// 广告统一入口（抖音小游戏 / AdSDK）。
///
/// 保留原有对外接口：ShowInterstitialAd / ShowRewardedAd / GiveReward，
/// 场景里按钮上的 UnityEvent 绑定（GameManager、IAPShopManager）不需要改动。
///
/// 通道说明：
///   - 抖音：走 Assets/AdSDK 的 AdManager（TikTokPlatform）
///   - 编辑器：不请求真广告，直接发奖，方便调试
///   - AdMob / Unity Ads：已停用（场景里 usingAdmobAds / usingUnityAds 均已置 0）
/// </summary>
public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance;

    [Header("平台开关")]
    [Tooltip("使用抖音广告（AdSDK/AdManager）。AppId 在 TikTokAdBootstrap.cs 里配置。")]
    public bool usingTikTokAds = true;

    [Tooltip("【已停用】旧 AdMob 通道，保留字段只为兼容场景序列化")]
    public bool usingAdmobAds;

    [Tooltip("【已停用】旧 Unity Ads 通道，保留字段只为兼容场景序列化")]
    public bool usingUnityAds;

    [SerializeField] private int rewardCoinsAmount;

    /// <summary>抖音广告是否可用</summary>
    private static bool TikTokReady
    {
        get
        {
#if UNITY_EDITOR
            return false;
#else
            return AdManager.Instance != null;
#endif
        }
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    #region 插屏广告

    /// <summary>
    /// Null 安全的插屏调用：AdsManager 尚未就绪（比如直接从 Gameplay 场景启动调试）时静默跳过。
    /// 供 GameManager / IAPShopManager 使用，避免写 AdsManager.Instance.XXX 时抛空引用 ——
    /// 本工程 WebGL 异常支持为 None，一个 NRE 会让整个游戏 abort。
    /// </summary>
    public static void TryShowInterstitial()
    {
        if (Instance == null)
        {
            Debug.Log("[广告] 插屏跳过：AdsManager 未就绪");
            return;
        }

        Instance.ShowInterstitialAd();
    }

    /// <summary>展示插屏广告（关卡切换、返回主页时调用）</summary>
    public void ShowInterstitialAd()
    {
        if (!usingTikTokAds) return;

        if (TikTokReady)
            AdManager.Instance.ShowInterstitialAd();
        else
            Debug.Log("[广告] 插屏跳过：抖音广告未就绪或当前为编辑器环境");
    }

    #endregion

    #region 激励视频

    /// <summary>展示激励视频，看完发默认奖励（场景 RewardVideoBtn 绑定的是这个方法）</summary>
    public void ShowRewardedAd()
    {
        ShowRewardedAd(GiveReward,
            error => Debug.LogWarning("[广告] 激励视频失败：" + error));
    }

    /// <summary>展示激励视频，自定义成功/失败回调</summary>
    public void ShowRewardedAd(Action onSuccess, Action<string> onFail)
    {
        if (!usingTikTokAds)
        {
            onFail?.Invoke("抖音广告通道已关闭");
            return;
        }

#if UNITY_EDITOR
        // 编辑器下不拉真实广告，直接走发奖逻辑，便于调试
        Debug.Log("[广告] 编辑器环境：跳过激励视频，直接发奖");
        onSuccess?.Invoke();
#else
        if (TikTokReady)
        {
            AdManager.Instance.ShowRewardedVideoAd(
                () => onSuccess?.Invoke(),
                error => onFail?.Invoke(error));
        }
        else
        {
            Debug.LogError("[广告] 抖音广告未就绪：请检查 TikTokAdBootstrap 里的 AppId / 广告位 ID");
            onFail?.Invoke("广告未就绪");
        }
#endif
    }

    /// <summary>看一次激励视频领取指定数量金币（商店按钮用）</summary>
    public void ShowRewardedAdForCoins(int coins)
    {
        ShowRewardedAd(
            () =>
            {
                if (CoinsManager.Instance != null)
                    CoinsManager.Instance.AddCoins(coins);
                if (AudioManager.Instance != null)
                    AudioManager.Instance.Play("Buy");
            },
            error =>
            {
                Debug.LogWarning($"[广告] 未获得 {coins} 金币：{error}");
                if (Toast.Instance != null)
                    Toast.Instance.ShowToast("广告未看完");
            });
    }

    /// <summary>发放默认激励奖励（原 UnityAds/AdMob 回调里调的就是这个方法）</summary>
    public void GiveReward()
    {
        if (CoinsManager.Instance != null)
            CoinsManager.Instance.AddCoins(rewardCoinsAmount);
    }

    #endregion

    #region 侧边栏 / 分享（抖音能力）

    /// <summary>跳转抖音侧边栏</summary>
    public void ShowSidebar(Action onSuccess = null)
    {
        if (!TikTokReady)
        {
            Debug.Log("[广告] 侧边栏跳过：抖音广告未就绪或当前为编辑器环境");
            return;
        }

        AdManager.Instance.NavigateToScene(
            () => onSuccess?.Invoke(),
            error => Debug.LogWarning("[广告] 侧边栏打开失败：" + error));
    }

    /// <summary>分享游戏</summary>
    public void ShareGame(Action<bool> callBack = null)
    {
        if (!TikTokReady)
        {
            Debug.Log("[广告] 分享跳过：抖音广告未就绪或当前为编辑器环境");
            return;
        }

        AdManager.Instance.ShareGame(callBack);
    }

    #endregion
}
