using TMPro;
using UnityEngine;
//电子邮件puhalskijsemen@gmail.com
//源码网站 开vpn全局模式打开 https://web3incubators.com/
//电报https://t.me/gamecode999
//网页客服 https://web3incubators.com/kefu.html
public class Toast : MonoBehaviour
{
    public static Toast Instance;
    public Animator toastAnim;
    public TMP_Text text;

    private void Awake()
    {
        if (Instance != null)
            Destroy(gameObject);
        else
            Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    public void ShowToast(string message)
    {
        AudioManager.Instance.Play("Toast");
        text.text = message;
        toastAnim.SetTrigger("Toast");
    }
}
