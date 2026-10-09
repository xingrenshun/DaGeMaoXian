using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Btn_Share : MonoBehaviour
{
    public void OnShareClick()
    {
        if (AdManager.Instance != null)
        {
            AdManager.Instance.ShareGame((isSuccess) =>
            {
                if (isSuccess)
                {
                    Debug.Log("分享成功");
                    // 可添加分享成功的奖励逻辑（如发放金币）
                }
                else
                {
                    Debug.Log("分享失败或取消");
                }
            });
        }
    }
}