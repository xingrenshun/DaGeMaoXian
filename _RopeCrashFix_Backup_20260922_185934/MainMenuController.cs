// MainMenuController.cs
// main 场景（标题页）控制器：只有点击「开始游戏」按钮才会进入主场景 Gameplay。
//
// 为什么不用鼠标点击整个屏幕都能进：
//   标题页上的 Panel / bg 都是 Image，把它们的 Raycast Target 关掉后，
//   整屏只有「开始游戏」按钮能接收点击，其它位置点下去不会有任何反应。
//
// 双重保险：
//   1) 编辑器工具 Assets/MyAssets/Editor/MainSceneSetup.cs 会把 StartGame 正式写进
//      按钮的 OnClick（Inspector 里看得见），并把它登记成首个 Build 场景；
//   2) 本脚本自带运行时兜底 —— 只要 main 场景被加载，就检查按钮有没有绑定，
//      没有就自动接上。所以即使 OnClick 被误清空，按钮依然可用。

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    /// <summary>标题页场景名（对应 Assets/MyAssets/Scene/main.unity）。</summary>
    public const string MainSceneName = "main";

    /// <summary>主场景（实际玩法）场景名。</summary>
    public const string GameplaySceneName = "Gameplay";

    [Tooltip("点击「开始游戏」后要加载的场景名")]
    [SerializeField] private string gameplaySceneName = GameplaySceneName;

    [Tooltip("留空则自动在场景里找名为 paly 的按钮")]
    [SerializeField] private Button startButton;

    /// <summary>防止连点导致重复 LoadScene。</summary>
    private bool loading;

    private void Awake()
    {
        EnsureStartButton();
    }

    /// <summary>
    /// 供 Button.onClick 调用：进入主场景。
    /// </summary>
    public void StartGame()
    {
        if (loading)
            return;
        loading = true;

        // 上一局可能停在暂停（Time.timeScale = 0），进场景前先恢复
        Time.timeScale = 1f;

        string target = string.IsNullOrEmpty(gameplaySceneName) ? GameplaySceneName : gameplaySceneName;

        // 走统一入口：先清掉所有 DOTween 补间再跳（标题页的按钮动画同理）
        RopeSceneGuard.Load(target);
    }

    /// <summary>
    /// 幂等：只有按钮上「没有任何持久绑定」时才补一个运行时监听，
    /// 避免和 Inspector 里已经配好的 OnClick 重复触发。
    /// </summary>
    public void EnsureStartButton()
    {
        if (startButton == null)
            startButton = FindStartButton();

        if (startButton == null)
        {
            Debug.LogWarning("[主菜单] 没找到开始游戏按钮（期望节点名 paly）");
            return;
        }

        if (startButton.onClick.GetPersistentEventCount() == 0)
        {
            startButton.onClick.AddListener(StartGame);
            Debug.Log("[主菜单] 已自动为按钮接上 StartGame：" + PathOf(startButton.transform));
        }
    }

    /// <summary>
    /// 优先按名字找 paly；找不到就退化为「当前场景里唯一的 Button」。
    /// </summary>
    private static Button FindStartButton()
    {
        Button[] all = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Scene active = SceneManager.GetActiveScene();

        Button only = null;
        int count = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].gameObject.scene != active)
                continue;

            count++;
            only = all[i];
            if (all[i].name == "paly")
                return all[i];
        }

        return count == 1 ? only : null;
    }

    private static string PathOf(Transform t)
    {
        string s = t.name;
        Transform p = t.parent;
        while (p != null)
        {
            s = p.name + "/" + s;
            p = p.parent;
        }
        return "/" + s;
    }

    // ------------------------------------------------------------------ 运行时兜底

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != MainSceneName)
            return;

        MainMenuController ctrl = Object.FindFirstObjectByType<MainMenuController>();
        if (ctrl == null)
        {
            GameObject canvas = GameObject.Find("Canvas");
            GameObject host = canvas != null ? canvas : new GameObject("MainMenuController");
            ctrl = host.AddComponent<MainMenuController>();
        }

        ctrl.EnsureStartButton();
    }
}
