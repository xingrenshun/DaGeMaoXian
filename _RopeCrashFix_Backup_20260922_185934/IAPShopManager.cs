using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 金币商店。
///
/// 抖音小游戏没有内购（Unity IAP / AdMob 已拆掉），所以商店改成
/// 「看一次激励视频，领对应数量的金币」。
///
/// 场景里 5 个商品按钮上的 IAPButton 组件已停用（m_Enabled: 0，不再触发内购），
/// 改成由 Button.onClick 直接调用 PurchaseComplete(金币数)。
/// </summary>
public class IAPShopManager : MonoBehaviour
{
    /// <summary>标题页场景名（对应 Assets/MyAssets/Scene/main.unity）。</summary>
    public const string MainSceneName = "main";

    /// <summary>主场景（实际玩法）场景名。</summary>
    public const string GameplaySceneName = "Gameplay";

    private void Awake()
    {
        Time.timeScale = 1;
    }

    /// <summary>
    /// 看广告领取对应金币。
    /// 方法名保持不变，因为 CoinShop 场景里的按钮还在调用它。
    /// </summary>
    public void PurchaseComplete(int coins)
    {
        if (AdsManager.Instance == null)
        {
            Debug.LogWarning("[商店] AdsManager 未找到，无法领取金币");
            return;
        }

        AdsManager.Instance.ShowRewardedAdForCoins(coins);
    }

    /// <summary>
    /// 金币商店的「主页」按钮。
    ///
    /// 参数保留只是为了不动场景里已有的按钮绑定（现在传进来的是 0）。
    /// 注意：不能直接 LoadScene(sceneIndex) —— main 现在是启动场景（Build 索引 0），
    /// 按索引跳会跳到别的地方，所以这里固定按场景名跳。
    /// 想让「主页」回 Gameplay 的话，把下面的 MainSceneName 换成 GameplaySceneName 即可。
    /// </summary>
    public void Home(int sceneIndex)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play("Click");

        AdsManager.TryShowInterstitial();

        // 走统一入口：先清掉所有 DOTween 补间再跳，避免补间在场景销毁后写已销毁的 Transform
        RopeSceneGuard.Load(MainSceneName);
    }
}
