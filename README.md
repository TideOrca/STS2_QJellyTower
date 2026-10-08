# Q弹尖塔

为杀戮尖塔2制作的娱乐mod，详见 <https://www.bilibili.com/video/BV1X1H26CEvz/>

## 安装

**需要先装前置模组 [BaseLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3737335127)。**

方式一：直接从 [创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3815412863) 订阅。

方式二：从 [Releases](../../releases) 下载 `QJellyTower-v1.0.0.zip`，解压得到
`Q弹尖塔.dll`、`Q弹尖塔.json`、`Q弹尖塔.pck` 三个文件，放进 `<游戏目录>/mods/Q弹尖塔/`。

> 打包成 zip 是因为 GitHub 的 Release 附件名只接受 ASCII 字符，
> 中文名会被服务端过滤掉（`Q弹尖塔.dll` 变成 `Q.dll`），交给 zip 就没有这个问题。

## 功能

纯视觉表现，**不修改任何游戏数值**。

- 战斗中打开开关后，我方与敌方角色做起伏的果冻式弹动
- 可选：角色左右镜像翻转、手牌与选卡界面、商店货品与商人弹动
- 弹动期间循环播放 BGM，音量可调
- 快捷键 **Q** 切换总开关（也可点顶栏按钮，鼠标悬停能看提示）

全部选项在游戏内「设置 → 模组配置 → Q弹尖塔」。

## 从源码构建

需要 Godot 4.5.x（.NET 版）与 .NET 9 SDK。

游戏程序集不随仓库分发，需要自己从游戏目录取出，放到 `../dll依赖/beta特供/`：
`sts2.dll`、`BaseLib.dll`、`GodotSharp.dll`、`0Harmony.dll`。

```bash
dotnet build

GODOT=path/to/Godot_v4.5.1-stable_mono_win64_console.exe
"$GODOT" --headless --path . --import
"$GODOT" --headless --path . --export-pack "Windows Desktop" "build/Q弹尖塔.pck"
```

产物在 `build/`。

## 目录结构

```
QJellyTowerCode/
  Config/    模组配置项（BaseLib SimpleModConfig）
  Core/      动画驱动、缩放施加、缓动、音频
  Patches/   Harmony 补丁（顶栏注入、快捷键、选卡界面）
  UI/        顶栏开关按钮
Q弹尖塔/
  audio/         BGM
  images/ui/     顶栏图标
  localization/  中英文文案
```
