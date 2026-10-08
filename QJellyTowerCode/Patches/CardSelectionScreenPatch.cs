using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace QJellyTower.Patches
{
    /// <summary>
    /// 战斗胜利后的卡牌奖励界面。它的卡牌行是动态往 UI/CardRow 里塞 NGridCardHolder 的，
    /// 所以这里只登记行节点，具体卡片由驱动每 0.1 秒重新取一次。
    /// </summary>
    [HarmonyPatch(typeof(NCardRewardSelectionScreen), "_Ready")]
    public static class CardRewardScreenPatch
    {
        [HarmonyPostfix]
        public static void Postfix(NCardRewardSelectionScreen __instance)
        {
            RegisterRow(__instance, "UI/CardRow", nameof(NCardRewardSelectionScreen));
        }

        internal static void RegisterRow(Node screen, string path, string screenName)
        {
            try
            {
                Control row = screen.GetNodeOrNull<Control>(path);
                if (row == null)
                {
                    Log.Warn("[Q弹尖塔] " + screenName + ": card row '" + path + "' not found; cards there won't jiggle.", 2);
                    return;
                }

                JellyDriver.RegisterCardRow(row);
            }
            catch (Exception ex)
            {
                Log.Error("[Q弹尖塔] " + screenName + ": failed to register card row: " + ex, 2);
            }
        }
    }

    /// <summary>事件/药水那种「选一张卡」界面，结构相同但卡牌行挂在根下。</summary>
    [HarmonyPatch(typeof(NChooseACardSelectionScreen), "_Ready")]
    public static class ChooseACardScreenPatch
    {
        [HarmonyPostfix]
        public static void Postfix(NChooseACardSelectionScreen __instance)
        {
            CardRewardScreenPatch.RegisterRow(__instance, "CardRow", nameof(NChooseACardSelectionScreen));
        }
    }
}
