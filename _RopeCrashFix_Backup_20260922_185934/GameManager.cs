using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;
using System;
using System.Collections;

public class GameManager : MonoBehaviour
{
    [SerializeField] private GameObject retryPanel, winPanel, startPanel, commingSoonPanel, spinWheelPanel;
    public Text levelText;
    private bool isWin, isRetry;
    private int levelNo;
    public int skipLevelCoinAmount;
    [HideInInspector] public bool gameStarted;

    // ---- 场景名（与 Build Settings 的排列顺序解耦）----
    // main 现在是启动场景（Build 索引 0），所以不能再写死 LoadScene(0)，
    // 否则「下一关」会跳回标题页。改成按名字加载后，以后调整场景顺序也不会跳错。
    public const string MainSceneName = "main";
    public const string GameplaySceneName = "Gameplay";
    public const string CoinShopSceneName = "CoinShop";

    /// <summary>场景里 OpenScene(int) 传进来的序号对应的场景名（0 = 重开本关, 1 = 金币商店）。</summary>
    [SerializeField] private string[] openSceneNames = { GameplaySceneName, CoinShopSceneName };

    void Awake()
    {
        Time.timeScale = 1;
        Application.targetFrameRate = 60;
        levelNo = PlayerPrefs.GetInt("Level", 0);
        FindFirstObjectByType<LevelSpawner>().SpawnLevel(levelNo);
        string levelName = "第 " + (levelNo + 1) + " 关";
        levelText.text = levelName;
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
            Time.timeScale = 1;

        if (Input.GetKeyDown(KeyCode.P))
            PlayerPrefs.DeleteAll();

        if (Input.GetKeyDown(KeyCode.R))
            RopeSceneGuard.ReloadCurrent();

        if (Input.GetKeyDown(KeyCode.N))
            Next();

        if (Input.GetKeyDown(KeyCode.M))
        {
            PlayerPrefs.SetInt("Level", 20+levelNo);
            RopeSceneGuard.Load(GameplaySceneName);
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            PlayerPrefs.SetInt("Level", --levelNo);
            RopeSceneGuard.Load(GameplaySceneName);
        }
    }

    public void Retry()
    {
        if (isWin)
            return;

        isRetry = true;
        Invoke(nameof(InvokeRetryGame), .5f);
    }

    private void InvokeRetryGame()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play("Retry");
        retryPanel.SetActive(true);
        retryPanel.transform.DOScale(new Vector3(1, 1, 1), .5f).SetLink(retryPanel);
    }

    public void Reload()
    {
        PlayClickSound();
        AdsManager.TryShowInterstitial();
        RopeSceneGuard.ReloadCurrent();
    }

    public void StartGame()
    {
        PlayClickSound();
        gameStarted = true;
        startPanel.SetActive(false);
    }

    public void Win()
    {
        if (isRetry)
            return;

        isWin = true;
        Invoke(nameof(InvokeWinGame), 1);
    }

    private void InvokeWinGame()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play("Win");

        // SetLink(面板)：面板被销毁时 DOTween 自动回收这条补间。
        // 否则「通关面板还在放大时点下一关」→ 场景销毁 → 补间下一帧仍去写
        // 已销毁 Transform 的 localScale → 崩（详见 RopeSceneGuard 顶部注释）。
        if (levelNo < 839)
        {
            winPanel.SetActive(true);
            winPanel.transform.DOScale(new Vector3(1, 1, 1), .5f).SetLink(winPanel);
        }
        else
        {
            commingSoonPanel.SetActive(true);
            commingSoonPanel.transform.DOScale(new Vector3(1, 1, 1), .5f).SetLink(commingSoonPanel);
        }
    }

    public void Next()
    {
        PlayClickSound();
        AdsManager.TryShowInterstitial();

        // ⚠ 跳转一律走 RopeSceneGuard：它会先 Killing 掉所有 DOTween 补间再切场景。
        //   直接 LoadScene 会让「金币飞行动画 / 通关面板 DOScale」在场景销毁后继续更新，
        //   对已销毁的 Transform 写 localScale → MissingReferenceException →
        //   本工程 WebGL 异常支持为 None，异常会直接 abort WASM，表现就是「点下一关卡死」。
        PlayerPrefs.SetInt("Level", ++levelNo);
        RopeSceneGuard.Load(GameplaySceneName);
    }

    /// <summary>「更多关卡 敬请期待」面板上的「主页」按钮：清档并回到标题页 main。</summary>
    public void ResetGame()
    {
        PlayerPrefs.DeleteAll();
        RopeSceneGuard.Load(MainSceneName);
    }

    public void ShowRewardVideo()
    {
        AdsManager.Instance.ShowRewardedAd();
    }

    public void OpenScene(int index)
    {
        PlayClickSound();
        AdsManager.TryShowInterstitial();

        // 场景里传的是「列表序号」而不是 Build 索引，避免被 Build Settings 顺序影响
        string[] map = (openSceneNames != null && openSceneNames.Length > 0)
            ? openSceneNames
            : new[] { GameplaySceneName, CoinShopSceneName };

        if (index < 0 || index >= map.Length || string.IsNullOrEmpty(map[index]))
        {
            Debug.LogWarning("[GameManager] OpenScene 参数越界：" + index + "，改为重开当前场景");
            RopeSceneGuard.Load(SceneManager.GetActiveScene().name);
            return;
        }

        RopeSceneGuard.Load(map[index]);
    }

    public void SkipLevel()
    {
        PlayClickSound();
        AdsManager.TryShowInterstitial();

        if (CoinsManager.Instance != null && CoinsManager.Instance.collectedCoins >= skipLevelCoinAmount)
        {
            CoinsManager.Instance.LessCoins(skipLevelCoinAmount);
            Next();
            if (AudioManager.Instance != null)
                AudioManager.Instance.Play("Buy");
        }
        else
        {
            if (Toast.Instance != null)
                Toast.Instance.ShowToast("金币不足");
        }
    }

    public void SetActiveSpinWheelPanel(bool val)
    {
        PlayClickSound();
        spinWheelPanel.SetActive(val);
    }

    /// <summary>
    /// 按钮点击音效。
    /// AudioManager 还没就绪时静默跳过 —— 本工程 WebGL 关掉了异常支持，
    /// 一个空引用就会 abort 掉整个 WASM（表现是整局卡死），所以单例访问一律加空判断。
    /// </summary>
    public void PlayClickSound()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play("Click");
    }
}