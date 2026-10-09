using UnityEngine;
using GoogleMobileAds.Api;
//电子邮件puhalskijsemen@gmail.com
//源码网站 开vpn全局模式打开 https://web3incubators.com/
//电报https://t.me/gamecode999
//网页客服 https://web3incubators.com/kefu.html
public class AdmobAdsManager : MonoBehaviour
{
    private string intertestialId = "ca-app-pub-3940256099942544/1033173712";
    private string bannerId = "ca-app-pub-3940256099942544/6300978111";
    private string rewardId = "ca-app-pub-3940256099942544/5224354917";

    private InterstitialAd interstitialAd;
    private BannerView bannerView;
    private RewardedAd rewardedAd;

    void Start()
    {
        if (!AdsManager.Instance.usingAdmobAds) return;

        MobileAds.Initialize(initStatus => { });
        ShowBannerAd();
        LoadInterstitialAd();
        LoadRewardedAd();
    }

    public void ShowBannerAd()
    {
        if (bannerView == null)
            CreateBannerView();

        var adRequest = new AdRequest();
        adRequest.Keywords.Add("unity-admob-sample");

        Debug.Log("Loading banner ad.");
        bannerView.LoadAd(adRequest);
    }

    private void CreateBannerView()
    {
        Debug.Log("Creating banner view");

        if (bannerView != null)
            DestroyAd();

        bannerView = new BannerView(bannerId, AdSize.Banner, AdPosition.Bottom);
    }

    private void DestroyAd()
    {
        if (bannerView != null)
        {
            Debug.Log("Destroying banner ad.");
            bannerView.Destroy();
            bannerView = null;
        }
    }

    private void LoadInterstitialAd()
    {
        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
            interstitialAd = null;
        }

        Debug.Log("Loading the interstitial ad.");

        var adRequest = new AdRequest();
        adRequest.Keywords.Add("unity-admob-sample");

        InterstitialAd.Load(intertestialId, adRequest,
            (InterstitialAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogError("interstitial ad failed to load an ad " +
                                   "with error : " + error);
                    return;
                }

                Debug.Log("Interstitial ad loaded with response : "
                          + ad.GetResponseInfo());

                interstitialAd = ad;
            });
    }

    public void ShowInterstitialAd()
    {
        if (interstitialAd != null && interstitialAd.CanShowAd())
        {
            Debug.Log("Showing interstitial ad.");
            interstitialAd.Show();
        }

        LoadInterstitialAd();
    }

    private void LoadRewardedAd()
    {
        if (rewardedAd != null)
        {
            rewardedAd.Destroy();
            rewardedAd = null;
        }

        Debug.Log("Loading the rewarded ad.");

        var adRequest = new AdRequest();
        adRequest.Keywords.Add("unity-admob-sample");

        RewardedAd.Load(rewardId, adRequest,
            (RewardedAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogError("Rewarded ad failed to load an ad " +
                                   "with error : " + error);
                    return;
                }

                Debug.Log("Rewarded ad loaded with response : "
                          + ad.GetResponseInfo());

                rewardedAd = ad;
            });
    }

    public void ShowRewardedAd()
    {
        if (rewardedAd != null && rewardedAd.CanShowAd())
        {
            rewardedAd.Show((Reward reward) =>
            {
                AdsManager.Instance.GiveReward();
            });
        }

        LoadRewardedAd();
    }
}