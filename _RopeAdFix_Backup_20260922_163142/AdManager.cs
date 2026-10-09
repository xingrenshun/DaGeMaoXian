using System;
using System.Collections.Generic;
using UnityEngine;

// ���ƽ̨����ö��
public enum AdPlatformType
{
    [InspectorName("�������")]
    TickTok,
    [InspectorName("TopOn���")]
    TopOn,
    [InspectorName("΢�Ź��")]
    WebChat,
}


/// <summary>
/// ������
/// </summary>
public class AdManager : MonoBehaviour
{
   // public GameObject aaa;
    public static AdManager Instance;
    [Header("ƽ̨")]
    public AdPlatformType adPlatformType = AdPlatformType.TickTok;
    [Header("С��ϷID")]
    public string AppId;
    [Header("�������λ")]
    public List<string> videoAdIdList = new();
    [Header("�������λ")]

    public List<string> interAdIdList = new();
    private IAdPlatform platFormIns = null;
    private float lastAdClickTime;

     void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (this.AppId == "")
        {
            Debug.LogWarning("请填写APPID");
        }
        
        if (this.videoAdIdList == null || this.videoAdIdList.Count == 0)
        {
            Debug.LogWarning("视频广告位ID列表为空, 将使用默认广告位");
        }

        if (this.interAdIdList == null || this.interAdIdList.Count == 0)
        {
            Debug.LogWarning("插屏广告位ID列表为空, 将使用默认广告位");
        }

        switch (adPlatformType)
        {
            case AdPlatformType.TickTok:
                platFormIns = new TikTokPlatform(this.AppId, this.GetRandVideoAdId(), this.GetRandScreenAdId());

                break;
            case AdPlatformType.TopOn:
                Debug.LogWarning("TopOn广告平台尚未实现");
                break;
        }
        
        // 添加广告平台初始化检查
        if (this.platFormIns == null)
        {
            Debug.LogError($"未初始化广告平台：{this.adPlatformType}");
        }
        
        this.lastAdClickTime = Time.time;
    }

    /// <summary>
    /// ���Ų������
    /// </summary>
    public void ShowInterstitialAd()
    {
        this.platFormIns?.ShowInterstitialAd(this.GetRandScreenAdId());
    }

    /// <summary>
    /// 跳转场景
    /// </summary>
    /// <param name="onSuccess">成功回调</param>
    /// <param name="onFail">失败回调</param>
    public void NavigateToScene(Action onSuccess = null, Action<string> onFail = null)
    {
        if (this.platFormIns == null)
        {
            Debug.LogError("广告平台实例未初始化，无法跳转场景");
            onFail?.Invoke("广告平台实例未初始化");
            return;
        }
        
        try
        {
            this.platFormIns.NavigateToScene(onSuccess, onFail);
        }
        catch (Exception e)
        {
            Debug.LogError($"跳转场景时发生异常：{e.Message}");
            onFail?.Invoke($"跳转场景时发生异常：{e.Message}");
        }
    }
    public void ShareGame(Action<bool> callBack = null)
    {
        this.platFormIns?.ShareGame(callBack);
    }

    /// <summary>
    /// ������Ƶ���
    /// </summary>
    /// <param name="onSuccess">�ɹ��ص�</param>
    /// <param name="onFail">ʧ�ܻص�</param>
    public void ShowRewardedVideoAd(Action onSuccess, Action<string> onFail)
    {
        if (!this.CheckTimeLimit())
            return;
        
        if (this.platFormIns == null)
        {
            Debug.LogError("广告平台实例未初始化，无法显示广告");
            onFail?.Invoke("广告平台实例未初始化");
            return;
        }
        
        try
        {
            this.platFormIns.ShowRewardedVideoAd(this.GetRandVideoAdId(), onSuccess, onFail);
        }
        catch (Exception e)
        {
            Debug.LogError($"显示广告时发生异常：{e.Message}");
            onFail?.Invoke($"显示广告时发生异常：{e.Message}");
        }
    }


    // ��ȡ����������ID
    private string GetRandVideoAdId()
    {
        if (this.videoAdIdList.Count == 0)
        {
            Debug.LogWarning("videoAdIdList is empty, using default ad id");
            return "f32bf9970ah83mq1kg"; // 默认广告ID，防止空字符串
        }
        int index = UnityEngine.Random.Range(0, this.videoAdIdList.Count);
        return this.videoAdIdList[index];
    }

    // ��ȡ����Ĳ������ID
    private string GetRandScreenAdId()
    {
        if (this.interAdIdList.Count == 0)
        {
            Debug.LogWarning("interAdIdList is empty, using default ad id");
            return "dk217ga5e5b48uo4gh"; // 默认广告ID，防止空字符串
        }
        int index = UnityEngine.Random.Range(0, this.interAdIdList.Count);
        return this.interAdIdList[index];
    }

    // ����x���ڹ��ֻ�������һ��
    private bool CheckTimeLimit()
    {
        if (Time.time - lastAdClickTime < 0.5f)
        {
            Debug.LogWarning("广告点击过于频繁，请稍后再试。");
            return false;
        }
        lastAdClickTime = Time.time;
        return true;
    }
}
