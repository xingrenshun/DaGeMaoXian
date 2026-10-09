using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景跳转统一入口：切场景之前先把 DOTween 的补间全部清掉。
///
/// ── 为什么必须这样（2026-09-22 真机卡死事故的根因）─────────────────────
/// 工程里大量用 DOTween 做动画，而这些补间**挂在场景对象上**：
///   GameManager   winPanel / retryPanel / commingSoonPanel 的 DOScale
///   CoinsAnimation 金币的 DOScale / DOMove / DORotate（还带 SetDelay，最长约 1.8s）
///   Roll          ropeRings 的 DOScale、RotateRoll 的无限循环 DORotate
///   KnotsHolder / Key / Column 的 DOMove / DOJump
///
/// 原来的代码在「关卡完成」面板上直接 SceneManager.LoadScene：
///   1. 通关瞬间金币动画 / 面板 DOScale 还在跑；
///   2. 玩家秒点「下一关」→ 场景被销毁，但这些补间还活着；
///   3. 下一帧 DOTween 继续更新补间 → 对**已销毁的 Transform** 写
///      localScale / position，抛 MissingReferenceException；
///   4. 而本工程 WebGL 的 Enable Exceptions = None，
///      **任何托管异常都会直接 abort 整个 WASM**，
///      于是开发者工具里出现：
///         Uncaught exception from main loop / Halting program
///         at requestAnimationFrame callback
///   5. 表现就是「点了下一关就卡死」，而且 Unity 侧没有任何可读报错。
///
/// 用 webgl.symbols.json 反查当时的崩溃栈，确认崩在：
///   Transform_set_localScale ← <DOScale>b__1 ← ShortcutExtensions(DO*)
/// 即「补间在场景销毁后写 Transform」，与上面的推理完全吻合。
///
/// ── 用法 ────────────────────────────────────────────────────────────
/// 以后所有跳转一律走 RopeSceneGuard，不要再直接调 SceneManager.LoadScene。
/// 想更保险的话，给单个补间挂 .SetLink(gameObject)，
/// 这样该物体被销毁时 DOTween 会自动回收这条补间。
/// </summary>
public static class RopeSceneGuard
{
    /// <summary>
    /// 切场景前必做的两件事：
    ///   1) 回收所有 DOTween 补间（否则补间会在新场景里对已销毁的 Transform 写入 → 崩溃）；
    ///   2) 恢复 Time.timeScale = 1
    ///      —— TikTokPlatform 播激励视频时会把它压成 0，
    ///         万一广告回调没走到 OnClose/OnError，时间会被永久冻住，
    ///         表现同样是「点了按钮就没反应」。这里兜一层。
    /// </summary>
    public static void PrepareForSceneChange()
    {
        // KillAll(complete: false)（默认就是不补完）：直接把补间回收掉，
        // 不强行把它们推到终点 —— 反正目标物体马上就要被销毁，推完也只是白写一次 Transform。
        // DOTween 还没初始化（比如标题页一次动画都没跑过）时它是安全空操作。
        DOTween.KillAll();
        Time.timeScale = 1f;
    }

    /// <summary>按场景名加载（会先清理补间）。</summary>
    public static void Load(string sceneName)
    {
        PrepareForSceneChange();
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>按 Build 索引加载（会先清理补间）。</summary>
    public static void Load(int buildIndex)
    {
        PrepareForSceneChange();
        SceneManager.LoadScene(buildIndex);
    }

    /// <summary>重开当前场景（会先清理补间）。</summary>
    public static void ReloadCurrent()
    {
        PrepareForSceneChange();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
