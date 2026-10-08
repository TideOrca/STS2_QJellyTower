using QJellyTower.UI;

namespace QJellyTower.Patches
{
    /// <summary>
    /// 顶栏构建完成时，把 Q 弹开关插到「牌堆」与「暂停」之间。
    /// 顶栏是进入游戏后第一个保证存在的节点，顺手在这里把动画驱动也装上。
    /// </summary>
    [HarmonyPatch(typeof(NTopBar), "_Ready")]
    public static class TopBarJellyButtonPatch
    {
        [HarmonyPostfix]
        public static void Postfix(NTopBar __instance)
        {
            try
            {
                JellyDriver.EnsureInstalled();

                Control right = __instance.GetNodeOrNull<Control>("RightAlignedStuff");
                if (right == null)
                {
                    Log.Warn("[Q弹尖塔] RightAlignedStuff not found on TopBar; jelly toggle not added.", 2);
                    return;
                }

                if (right.GetNodeOrNull<Control>("QJellyToggleButton") != null)
                {
                    return;
                }

                Control button = JellyToggleButton.Create();
                right.AddChild(button);

                // 挪到暂停按钮左侧，顺序变成：地图 / 牌堆 / Q弹开关 / 暂停
                Node pause = __instance.Pause;
                if (pause != null && GodotObject.IsInstanceValid(pause) && pause.GetParent() == right)
                {
                    right.MoveChild(button, pause.GetIndex());
                }

                Log.Info("[Q弹尖塔] top bar jelly toggle injected (active=" + QJellyTowerMain.JellyActive + ")", 2);
            }
            catch (Exception ex)
            {
                Log.Error("[Q弹尖塔] failed to inject top bar toggle: " + ex, 2);
            }
        }
    }
}
