# 工程长期记忆 · 打个毛线 Rope

> 路径 `D:\Unity\Unity6000_打个毛线_Rope\Unity6000_打个毛线_Rope\Unity6000_打个毛线_Rope\Unity6000_打个毛线_Rope`
> Unity 6000.2.15f1 · 抖音小游戏（主包上限 30MB）· ColorRope「绳/管排序」玩法
> ⚠ **上架名「宥如极速冒险」「昇辉极速冒险」「秘境寻踪奇旅」= 工程代号「打个毛线 Rope」，同一个项目。**
> 名字带"极速冒险/奇旅"但**玩法与竞速、探索、解谜冒险均无关**（是绳线配对整理），写文案一律以真实玩法为准。
> 三个上架名**全无副本目录**，三份 `*_作品介绍.txt` 都放本工程根，靠文件名区分，**措辞须判重**。
> ⚠ **命名风格会变**：前两个是「品牌前缀+极速冒险」，第三个「秘境寻踪奇旅」换成了**抽象诗意系**
> （与 maomidiaoyu / jiaotonzhihuiwang 等工程同名系风格）。**拿到这个名字曾无法判断归属**——
> 工程目录、历史文案、会话备份里全搜不到 `mjxzql` 副本，是靠问用户才确认的。
> **下次再遇到全新名字，先查副本目录 + `.modify_backup_meta`，查不到就直接问，别猜。**

## 版本控制

- 远程 `https://github.com/xingrenshun/DaGeMaoXian.git`（public），分支 `main`，`-u` 已关联。
  首个提交 `f545b91`（备注「源码」）= 2767 文件 / 50.6MB。
- **仓库根的 `.gitignore` / `.gitattributes` 是新增的，别删**：工程 2.0GB，`Library/` 独占 1.8GB、
  `tt-minigame/` 108MB，全靠它拦住；`.gitattributes` 把 `*.unity/*.prefab/*.asset/*.meta` 标 `-text` 防换行转换损坏 YAML。
- 本地已设 `core.autocrlf false`（全局为 true）。凭证走 `credential.helper=store`，免密。
  推送前先 `netstat -ano | grep 7897` 确认 Clash 代理在监听。

## 工程结构速查

| 位置 | 说明 |
|---|---|
| `MyAssets/Scripts/` | `LevelSpawner` `GameManager` `CoinsManager` `AdsManager` `IAPShopManager` `SpinWheelTimer` `RopeSceneGuard` |
| `MyAssets/Levels/` | 关卡 JSON **840 个**（`001-E`…`840-N`），单行紧凑，合计 12.8MB |
| `MyAssets/Scene/` | `main`（标题页·启动场景）+ `Gameplay`（玩法）+ `CoinShop` |
| `Assets/AdSDK/` | 自研广告封装 + `Font/`（中文 TTF）。⚠ **另一套同名目录存在于多个工程**，别只看目录名认工程 |
| `Assets/Plugins/ByteGame/com.bytedance.starksdk/` | 抖音 TTSDK 6.9.4 |
| `MyAssets/Editor/` | `ChineseFontBuilder` `RopeUITextRestore` `MainSceneSetup` `RopeSpriteModeFix` `RopeTTSDKBuildFix`（菜单都在 `Tools/打个毛线/`） |
| 构建产物 | `tt-minigame/tt-minigame/`（含 `webgl.symbols.json` ← 排查崩溃栈的利器） |

## 场景流程

`main`(Build 0，「开始游戏」按钮) → `Gameplay`(1) → `CoinShop`(2) → `main`

- **所有跳转按场景名，不依赖 Build 索引。** `OpenScene(int)` 走 `[SerializeField] string[] openSceneNames = { "Gameplay", "CoinShop" }` 做序号→名映射；**场景按钮传的还是 0/1，别改签名**（改了绑定变 Missing）。
- ⚠ **场景跳转一律走 `RopeSceneGuard.Load/Load(int)/ReloadCurrent()`，不要再裸调 `SceneManager.LoadScene`**（原因见「崩溃排查」）。

## 玩法事实（写商店文案/简介直接引用，840 关全量实测值）

竖屏单指点击、无角色无 NPC 无剧情无倒计时。台面铺彩色线轴 → 点入下方收纳槽 → 槽内颜色与上方某列
队列最前端一致则彩线牵引缠绕，**缠满 72 圈**整束消失（`Roll.cs:221 ropeCount/72`）。

- **可点规则**：上下左右任一格为空**或出界**即可点（`RollMovableManager.IsRollPathBlocked`）；
  开关在 `Roll.OnMouseDown` 的 `!isMovable || isFrozen || lockObj || !gameStarted`。
- **失败**：所有收纳槽非空 且 无任一队列前端颜色可配（`RollsManager.CheckRetryInvoke`）。
- **规模**（10-09 按真实 JSON 字段 `gridSize{x,y}` / `totalBlock` / `blocks` 重跑校正，**旧记录的"最大 8×8"不准**）：
  840 关；台面 **22 种尺寸**，x∈[2,9]、y∈[3,9]；**格子数最大是 8×8=64 格，但存在 9×6 / 9×7 / 7×9 的长条台面**，
  所以别写"最大 8×8"（会被 9 宽的关卡打脸），要写就写"尺寸共 22 种"。
  线轴 **6–64 个**（均值 41.4，= `len(blocks)`；`totalBlock` 最多 **78**，多出来的是管道运行时吐的）；
  每关配色 **2–11 种**（10 色 664 关、9 色 135 关、11 色 9 关）。
- **收纳槽**：`<4 关=4 个、4–11 关=5 个、12–23 关=6 个、≥24 关=7 个`（`LevelSpawner.SpawnRollContainers`）。
- **机关累计次数**（`LevelSpawner.SpawnBlocks` 的 `switch(blockData.type)` + `state` 字段）：遮布 2397（`isHidden`，`Roll.CanMove()` 里自动揭开）、
  冰块 334（`isFrozen`，多层逐层敲碎）、锁 84 / 钥匙 **66**（`isLocked`/`isKey`）、成对连环 `linkType==1` **641**；
  **管道 = `type==3`（1895 个，`pipePrefab`，`attachments[].color` 决定吐什么色）**、
  **固定障碍 = `state.color==0`（走 `SpawnObstacle`）**，两者**在关卡 JSON 里都没有 `Pipe_`/`Obstacle_` 字样**
  （那是运行时 `gameObject.name`）——**grep "Pipe" 搜不到不等于没这个机制**，要查 `type` 数值。
- **系统**：金币（初始 100）；每日免费转盘 24h **真发金币**；跳关 **150 金币**（`Gameplay.unity` 的 `skipLevelCoinAmount`）；
  「金币商店」实为**看广告换币**（`IAPShopManager.PurchaseComplete` → `ShowRewardedAdForCoins`）；音效 17 处。
- ⚠ **震动反馈在 WebGL/抖音上不生效**（`Vibration.cs` 只在 `RuntimePlatform.Android` 初始化 vibrator），**文案别写"震动"**。

## 关键约定（别踩）

- **崩溃排查：WebGL `Enable Exceptions` 必须是 `FullWithoutStacktrace`(2)。**
  原来是 `None`(0) → 任何托管异常**无法捕获、直接 abort 整个 WASM**，抖音工具只报
  `Uncaught exception from main loop` / `Halting program`，Unity 侧零可读报错，整局卡死。`try/catch` 也一并失效。
  已在 `ProjectSettings.asset` + `RopeBuildPreflight`（构建前强制）双处固定。代价：包体+几百 KB。
- **DOTween 补间必须与场景生命周期对齐。** 事故根因：补间挂在场景物体上，切场景后仍去写已销毁 Transform 的
  `localScale/position` → `MissingReferenceException` → 上面那条 `None` 把它变成整局卡死。
  对策：① 跳转走 `RopeSceneGuard`（内部 `DOTween.KillAll()` + `Time.timeScale=1`）；
  ② 长延迟/无限循环补间加 `.SetLink(gameObject)`——重点：`CoinsAnimation`（SetDelay 拖到通关后约 2.7s）、
  `Roll.RotateRoll`（`SetLoops(-1)` 永不自结束）、通关面板 `DOScale`。
  ⚠ 别再用 `AdsManager.Instance.XXX` 这类裸单例访问，用 `AdsManager.TryShowInterstitial()` / 加空判断。
- **广告权威配置：`main.unity` 场景里的 AdManager 组件**（9-22 晚用户已改为激励 `1s1dq6mujcl21i3b0o`、
  插屏 `1p205m3edko373l8g6`），`TikTokAdBootstrap` 只补空字段，**必须 `AfterSceneLoad`**——用 `BeforeSceneLoad`
  会抢跑占住单例，场景里配好的 AdManager 反而自我销毁 → 空配置实例 → 报 `The AdUnitId is invalid`(1002)。
  ⚠ 那两个写死的废弃 ID（`f32bf9970ah83mq1kg` / `dk217ga5e5b48uo4gh`）9-22 已删除。
- **appid 有两个互不相通的入口，极易改一个忘一个（9-22 已统一为 `tt8474f2c91a0fb1c002`）**：
  - 构建 appid = `Assets/Editor/StarkBuilderSetting.asset` 的 `_appId` → 进 `project.config.json`/`game.js`，决定产物归属；
  - 广告 appid = `TikTokAdBootstrap.AppId`（因为场景 AdManager 的 `AppId` 字段是空的，运行时由自举补）
    → 决定埋点/上报记到哪个小游戏；`TikTokPlatform` 构造函数的末级兜底也指向它。
  - 已在 `RopeBuildPreflight.OnPreprocessBuild` 加**构建前一致性校验**，不一致直接 `LogError` 打出两个值。
- **排查 1002 的 30 秒判据**：报错里的 adUnitId 与工程配的 ID **逐字**对比，对不上就说明生效的不是你配的那份；
  别急着去抖音后台查审核。⚠ `l`（小写L）与 `1` 极易看混 —— 9-22 我就把 `1p205m3edko373l8g6` 读成
  `...37318g6`，害得全工程搜不到、误判"跑的是旧包"。核对一律以**文本消息/文件 grep 结果**为准，别信截图转写。
- **构建产物可 grep 性**：`tt-minigame/webgl/Build/webgl.data`（16M，未压缩）**能**搜到场景文本与字符串；
  运行时用的 `tt-minigame/tt-minigame/data-package/*.unityweb.bin.br`（6.4M）是 Brotli 压缩，**搜不到**。
  判断"包里的 ID 对不对"要搜前者。
- **场景 UI 是旧版 `UnityEngine.UI.Text`，不是 TMP**。中文化靠换 `m_Font` GUID 指向 `DroidSansFallback.ttf`
  （GUID `4b55cb2df6c16834786823b918fe5b7f`），不需要 SDF 图集；只有 Toast 走 TMP，才要 `Chinese SDF.asset`。
  英文 UI 字体 `NEXON Football Gothic B.otf`（GUID `8cbaa696369b94278bbdef7eadd77c70`）子集后**无汉字**。
- ⚠⚠ **不要手写/正则改 `.unity` 的 YAML。** 9-18 就是这么把 `m_Font` 缩进写坏（应为 `m_FontData:` 下缩进 4，
  写成了 0）→ 反序列化失败 → **16 个 Text 组件整体回默认值**（文案空+字体 None+字号 14），保存即永久落盘。
  要改场景内容 → 写 Editor 脚本走 `GlobalObjectId` + 层级路径双键匹配（范式见 `RopeUITextRestore.cs`）。
- **`Sprite Mode=Multiple` 且 `spriteSheet.sprites: []`（0 切片）= 不生成任何 Sprite**（选不到、`Resources.Load` 返回 null）。
  单张图必须 `spriteMode: 1`（Single，fileID 固定 `21300000`）；改 Multiple 后 fileID 变哈希，**原引用全断**。
- ⚠ **Unity 开着时从外部改 `.meta` / `ProjectSettings.asset` 会被编辑器写回**。贴图一律走 Editor 脚本 +
  `TextureImporter.SaveAndReimport()`。
- **关卡是场景序列化引用**（`LevelSpawner.levelsJson : List<TextAsset>`），不在 Resources 下，靠场景引用进包。
- **字体文件一律不许删**（用户明确要求），要瘦身只做原地子集化 + 备份。
- **`Assets/Editor/StarkBuilderSetting.asset`（TTSDK 工具窗口读写）在 Unity 6 下必须是**：
  `enableEmscriptenGlx: 0`（TTSDK 只带 2021/2022 的 GLX 预编译库，Unity 6 无匹配 → 抛
  `Failed to activate emscripten glx`，包都产不出；GLX 是可选优化，关掉不影响上架）、
  `isWebGL2: 1`（Unity 6 只支持 WebGL 2.0；⚠ 本工程色彩空间是 Gamma，**不会**报 color space 错，别以为无关）。
  ⚠⚠ **"改一次"必复发**：TTSDK 每次点构建都会把它内存里的设置重新落盘（Editor.log 里
  `Start importing …StarkBuilderSetting.asset` 紧跟 `build begin` 即铁证），且**工具窗口根本没暴露 GLX 勾选框**，
  用户改不了 → 只能靠 `Editor/RopeTTSDKBuildFix.cs` 的**四层守卫**（L1 AutoFix 域重载 / L2 `OnWillSaveAssets`
  保存前 / L3 `OnPostprocessAllAssets` 导入后改内存实例=决定性一层 / L4 `EditorApplication.update` 兜底；
  L5 `RopeBuildPreflight` 管模板）。**别再说"关掉重开工具窗口"**（重开也拦不住）。
  改该文件前先读文件顶部注释与其实现禁忌（反射改字段、postprocess 不调 SaveAssets、`OnWillSaveAssets` 必须 `return paths`、构建中跳过）。
- **`webGLTemplate` 不能为空**（空值报 `No WebGL template selected!`，栈是 Unity 自己的 `/home/bokken/...`）。
  无自定义模板就填 `APPLICATION:Default`。⚠ 这条报错出现时**包体其实已经打好了**。
- `webGLCompressionFormat` **是 TTSDK 托管的，别手改**（`SetupWebGLBuildSettings` 会在构建时覆盖）。
- `AdManager.cs` 含 **110 个历史损坏的 U+FFFD**（早期编码事故），改它必须用 Python 精确替换并断言数量不变。

## 体积关键数字

- 进包资源构成：**纹理是大头**（≈21.8MB），其次关卡 JSON 12.8MB、字体 3.65MB、音频 2.23MB；
  优化后进包资源 27.72MB → 14.52MB，压缩后包体约 4.9MB（wasm 9MB + data 6.4MB + framework 0.5MB）。
- 两张巨型雪碧图 `Sprites/512 (1).png`(源 3584×4608)、`WinEmoji.png`(源 3584×5120)：`maxTextureSize` 2048→**1024**
  省约 4.6MB，代价是白底变软；要回 2048 只改 meta 重导入，包体 +4.6MB（≈23→≈28MB，仍 <30MB）。
  切片矩形存 `spriteSheet`、是源像素坐标，改 maxTextureSize **不会切错位**。
  **算贴图体积务必按 maxTextureSize 收敛，别用源尺寸。**
- ⚠ 构建产物里的 `webgl.symbols.json` 有 **12MB**（比 wasm 还大），上架前确认是否计入包体。
- `Chinese SDF.asset` 4.19MB：448 字形 @48pt/padding4 占 2 页 1024²；点大小降到 40 可单页省约 1MB（未做）。

## 备份 / 报告（回滚用，剩余在根目录 `_Rope*`）

- `_RopeScene_Backup_20260919_110241\Gameplay.unity` —— **未损坏原始版，恢复文案的唯一依据，别删**
- `_RopeMeta_Backup_20260919_110951\`(82 meta)、`_RopeFont_Backup_20260919_111034\`(字体原件)、
  `_RopeSceneBackup2_20260919_134234\CoinShop.unity`、`_RopeSpriteFix_Backup_20260920_192903\`、
  `_RopeGlx_Backup_20260921_103834\` + `_RopeGlxGuard_Backup_20260922_161543\`(StarkBuilderSetting)、
  `_RopeTemplate_Backup_20260921_104800\`(ProjectSettings)、`_RopeAdFix_Backup_20260922_163142\`(广告脚本+main.unity)、
  `_RopeCrashFix_Backup_20260922_185934\`(ProjectSettings + 6 个脚本)
- 报告（根目录 `_*.html`）：包体瘦身 / 场景文本修复 / main场景按钮 / 精灵图无法切换 / 构建报错 /
  WebGL模板缺失 / GLX构建报错复发 / 广告ID无效 / 下一关卡死崩溃
