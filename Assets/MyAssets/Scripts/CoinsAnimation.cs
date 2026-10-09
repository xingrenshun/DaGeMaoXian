using DG.Tweening;
using UnityEngine;

public class CoinsAnimation : MonoBehaviour
{
    [SerializeField] private GameObject pileOfCoins;
    //private Vector2[] initialPos;
    //private Quaternion[] initialRotation;
    [SerializeField] private int coinsAmount;
    private Vector2 targetPos;

    void Start()
    {             
        //initialPos = new Vector2[coinsAmount];
        //initialRotation = new Quaternion[coinsAmount];
        
        //for (int i = 0; i < pileOfCoins.transform.childCount; i++)
        //{
        //    RectTransform rectTransform = pileOfCoins.transform.GetChild(i).GetComponent<RectTransform>();

        //    initialPos[i] = rectTransform.anchoredPosition;
        //    initialRotation[i] = rectTransform.rotation;
        //}

        targetPos = CoinsManager.Instance.collectedCoinsText.transform.position;

        CountCoins();
    }

    public void CountCoins()
    {
        pileOfCoins.SetActive(true);
        var delay = 0f;

        for (int i = 0; i < pileOfCoins.transform.childCount; i++)
        {
            Transform coin = pileOfCoins.transform.GetChild(i);

            // ⚠ SetLink(coin.gameObject)：这是「通关后点下一关卡死」的头号嫌疑。
            //   这几条补间带 SetDelay，最后一枚金币的收尾动画要到通关后约 2.7 秒才结束。
            //   玩家在这之前点「下一关」→ 场景被销毁 → 补间下一帧仍去写已销毁 Transform
            //   的 localScale / position → MissingReferenceException。
            //   本工程 WebGL 异常支持是 None，异常会直接 abort WASM → 整局卡死。
            //   SetLink 让 coin 被销毁时 DOTween 自动回收这两条补间。
            coin.DOScale(.8f, 0.3f).SetDelay(delay).SetEase(Ease.OutBack).SetLink(coin.gameObject);
            coin.DOMove(targetPos, 0.8f).SetDelay(delay + 0.5f).SetEase(Ease.InBack).SetLink(coin.gameObject);
            coin.DORotate(Vector3.zero, 0.5f).SetDelay(delay + 0.5f).SetEase(Ease.Flash).SetLink(coin.gameObject);
            coin.DOScale(0f, 0.3f).SetDelay(delay + 1.5f).SetEase(Ease.OutBack).SetLink(coin.gameObject);

            delay += 0.1f;
        }

        Invoke(nameof(PlayCoinSound), 1);
        InvokeRepeating(nameof(GiveCoins), .1f, .1f);
    }


    private void PlayCoinSound()
    {
        AudioManager.Instance.Play("Coins");
    }

    int count = 0;
    void GiveCoins()
    {
        count++;

        if (count == 25)
            CancelInvoke(nameof(GiveCoins));

        CoinsManager.Instance.AddCoins(2);
    }
}
