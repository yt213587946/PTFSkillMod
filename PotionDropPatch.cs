using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using Wogame;

namespace PTFSkillMod
{
    // 道具图鉴界面：鼠标划过条目时输出ID
    // Boss击败后在地图上掉落指定药水
    public static class PotionDropPatch
    {
        private static int LEGEND_HAMMER_ID = 7314; // "传奇铁锤"的ID

        private static HashSet<int> _loggedIds = new HashSet<int>();

        //鼠标划过图鉴条目时输出ID+名称
        [HarmonyPatch(typeof(StoryLockItem), nameof(StoryLockItem.OnEnter))]
        [HarmonyPostfix]
        public static void StoryLockItem_OnEnter(StoryLockItem __instance)
        {
            if (__instance == null || __instance.Pointer == IntPtr.Zero) return;
            int itemId = __instance.itemID;
            if (itemId <= 0) return;
            if (_loggedIds.Contains(itemId)) return;
            _loggedIds.Add(itemId);
            string name = ResolveItemName(itemId);
            //SkillTreePlugin.ModLog.LogInfo("[PotionMod] 图鉴条目 ID=" + itemId + " 名称=" + name);
        }

        // 通过ItemData.GetConfig解析物品名称
        private static string ResolveItemName(int itemId)
        {
            try
            {
                var itemData = new ItemData(itemId, 1, 0u);
                if (itemData == null || itemData.Pointer == IntPtr.Zero) return "(ItemData创建失败)";
                var cfg = itemData.GetConfig();
                if (cfg == null || cfg.Pointer == IntPtr.Zero) return "(GetConfig返回null)";
                string name = cfg.Name ?? "(null)";

                // 药水的Name是本地化Key
                if (name.StartsWith("Item.Name."))
                {
                    var modeComp = GameEntry.Mode;
                    if (modeComp != null && modeComp.Pointer != IntPtr.Zero)
                    {
                        var configMode = modeComp.GetModel<ConfigMode>();
                        if (configMode != null && configMode.Pointer != IntPtr.Zero)
                        {
                            string localized = configMode.GetString(name);
                            if (!string.IsNullOrEmpty(localized))
                                name = localized;
                        }
                    }
                }

                return name;
            }
            catch (Exception ex)
            {
                return "(异常:" + ex.GetType().Name + ")";
            }
        }

        // Boss击败后掉落药水
        [HarmonyPatch(typeof(BattleForm), nameof(BattleForm.BossHpBarDead))]
        [HarmonyPostfix]
        public static void BossDead_DropPotion()
        {
            if (LEGEND_HAMMER_ID <= 0) return;
            try
            {
                var modeComp = GameEntry.Mode;
                if (modeComp == null || modeComp.Pointer == IntPtr.Zero) return;
                var iCombat = modeComp.CombatMode;
                if (iCombat == null || iCombat.Pointer == IntPtr.Zero) return;
                var cm = new CombatMode(iCombat.Pointer);

                // 在玩家位置附近掉落
                cm.DropItem(LEGEND_HAMMER_ID, Vector3.zero);
                //SkillTreePlugin.ModLog.LogInfo("[PotionMod] 掉落药水：传奇铁锤");
            }
            catch (Exception ex)
            {
                SkillTreePlugin.ModLog.LogWarning("[PotionMod] 掉落异常: " + ex.Message);
            }
        }
    }
}
