using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TTSDK.UNBridgeLib.LitJson;
using TTSDK;
/// <summary>
/// 抖音排行榜功能实现类
/// 提供简单的分数上传和排行榜显示功能
/// </summary>
public class PaiHangBang : MonoBehaviour
{
    // 单例模式实现
    public static PaiHangBang Instance;
    
    // 测试用的上传分数按钮
    public Button btn_UploadScore;
    // 排行榜面板
    public GameObject rankPanel;
   

    // 测试用的分数
    private int testScore = 100;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        if (btn_UploadScore != null)
            btn_UploadScore.onClick.AddListener(() => UploadScore(testScore++));

       // if (btn_Share != null)
           // btn_Share.onClick.AddListener(ShareGame);
    }

    /// <summary>
    /// 打开排行榜面板
    /// </summary>
    public void OpenRankPanel()
    {
        if (rankPanel != null)
            rankPanel.SetActive(true);
            
        // 显示排行榜数据
        ShowRankList();
    }
    // 显示排行榜数据
    public void ShowRankList_brn()
    {
       
        ShowRankList();
    }
    public void CheckSession(bool callShow = false, Action<bool> callback = null)
    {
        TT.CheckSession(() =>
        {
            Debug.Log("用户已登录，会话有效");
            if (callShow)
            {
                callback?.Invoke(true);
            }
        }, (err) =>
        {
            Debug.LogWarning($"会话无效，尝试重新登录: {err}");
            TT.Login((code, anonymousCode, isLogin) =>
            {
                if (isLogin)
                {
                    Debug.Log("登录成功");
                    if (callShow)
                    {
                        callback?.Invoke(true);
                    }
                }
                else
                {
                    Debug.LogError("登录失败，无法获取排行榜数据");
                    if (callShow)
                    {
                        callback?.Invoke(false);
                    }
                }
            },
            (msg) => { Debug.LogError($"登录失败: {msg}"); callback?.Invoke(false); }, true);
        });
    }

    /// <summary>
    /// 上传分数到抖音排行榜
    /// </summary>
    /// <param name="score">要上传的分数</param>
    /// <param name="zoneId">排行榜分区，默认使用"default"</param>
    public void UploadScore(int score, string zoneId = "default")
    {
        try
        {
            JsonData data = new JsonData()
            {
                ["dataType"] = 0,
                ["value"] = score.ToString(),
                ["priority"] = 0,
                ["zoneId"] = zoneId
            };
            CheckSession(true, (loginSuccess) =>
            {
                if (loginSuccess)
                {
                    TT.SetImRankData(data, (success, message) =>
                    {
                        if (success)
                        {
                            Debug.Log("上传数据成功");
                        }
                        else
                        {
                            Debug.Log($"上传数据失败: {message}");
                        }
                    });
                }
                else
                {
                    Debug.LogError("用户未登录，无法上传分数");
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"上传分数时发生错误：{e.Message}");
        }
    }

    /// <summary>
    /// 显示排行榜数据
    /// </summary>
    /// <param name="zoneId">排行榜分区，默认使用"default"</param>
    /// <param name="rankType">排行榜类型，1表示全局榜，2表示好友榜，默认全局榜</param>
    public void ShowRankList(string zoneId = "default", int rankType = 1)
    {
        try
        {
            CheckSession(true, (loginSuccess) =>
            {
                if (loginSuccess)
                {
                    GetRankList_fun(zoneId, rankType);
                }
                else
                {
                    Debug.LogError("用户未登录，无法获取排行榜数据");
                    // 可以在这里添加UI提示，告知用户需要登录才能查看排行榜
                }
            });
            
        }
        catch (Exception e)
        {
            Debug.LogError($"获取排行榜时发生错误：{e.Message}");
        }
    }
    
    /// <summary>
    /// 根据抖音API调用排行榜获取功能
    /// </summary>
    /// <param name="zoneId">排行榜分区ID</param>
    /// <param name="rankType">排行榜类型：1-全局榜，2-好友榜</param>
    private void GetRankList_fun(string zoneId = "default", int rankType = 1)
    {
        // 构建抖音API要求的排行榜请求参数
        JsonData param = new JsonData();
        
        // 必传参数
        param["zoneId"] = zoneId; // 排行榜分区ID
        param["dataType"] = 0; // 分数类型：0-数字类型，1-枚举类型
        
        // 可选参数
        param["relationType"] = rankType == 1 ? "default" : "friend"; // 关系类型：default-总榜，friend-好友榜
        param["rankType"] = "all"; // 时间范围：day、week、month、all
        param["pageNum"] = 1; // 页码，从1开始
        param["pageSize"] = 20; // 每页长度，大于0小于40
        
        // 调用抖音排行榜API (使用正确的API签名)
        TT.GetImRankList(param, (success, message) =>
        {
            if (success)
            {
                Debug.Log("排行榜数据获取成功");
                // 注意：当前SDK版本的GetImRankList方法只返回成功/失败状态，不返回详细数据
                // 需要使用其他方法获取详细排行榜数据
            }
            else
            {
                Debug.LogError($"排行榜数据获取失败: {message}");
            }
        });
    }

    /// <summary>
    /// 分享功能实现
    /// </summary>
    public void ShareGame()
    {
        try
        {
            // 构建分享参数
            JsonData shareData = new JsonData()
            {
                ["title"] = "快来试试这款有趣的游戏！",
                ["content"] = "我在SaveMyPet中取得了高分，快来挑战我吧！",
                ["imageUrl"] = "", // 分享图片URL
                ["shareType"] = 0 // 分享类型：0-图文分享
            };
            
            Debug.Log("准备分享游戏");
            
            // 调用抖音分享API
            TT.ShareAppMessage(shareData);
        }
        catch (Exception e)
        {
            Debug.LogError("分享游戏时发生错误：" + e.Message);
        }
    }
}