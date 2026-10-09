using UnityEngine;

/// <summary>
/// 「看广告得奖励」按钮。
///
/// 注意：原版 AdSDK 里这个脚本引用的是另一个工程（摸鱼打螺丝模板）的
/// ManagerMecanique / ManagerAds，本工程没有这两个类，会导致编译不过。
/// 这里改成通用实现：点一次看一次激励视频，然后发金币。
/// </summary>
public class Btn_Ad : MonoBehaviour
{
    [Tooltip("看完激励视频发放的金币数量")]
    [SerializeField] private int rewardCoins = 1500;

    public void Btn_AdFlashOnClick()
    {
        if (AdsManager.Instance == null)
        {
            Debug.LogWarning("[广告] AdsManager 未找到，无法播放激励视频");
            return;
        }

        AdsManager.Instance.ShowRewardedAdForCoins(rewardCoins);
    }
}
