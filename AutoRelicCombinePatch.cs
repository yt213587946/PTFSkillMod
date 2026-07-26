using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using Wogame;

namespace PTFSkillMod
{
    // 自动秘宝合成补丁
    // 检测玩家背包中的重复秘宝（相同RelicID 数量>=2），自动合成为高级版本
    public static class AutoRelicCombinePatch
    {
        // 防止递归/重入
        private static bool _isProcessing = false;
        // 记录已处理的实例ID组合
        private static HashSet<string> _processedPairs = new HashSet<string>();

        private static bool IsValid(Il2CppSystem.Object obj)
        {
            return obj != null && obj.Pointer != IntPtr.Zero;
        }

        private static bool IsValid(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase obj)
        {
            return obj != null && obj.Pointer != IntPtr.Zero;
        }

        #region Harmony Patches

        // 按Tab打开背包时触发扫描
        [HarmonyPatch(typeof(BattleSetForm), nameof(BattleSetForm.OnOpen))]
        [HarmonyPostfix]
        public static void BattleSetForm_OnOpen_Postfix()
        {
            var modeComp = GameEntry.Mode;
            if (!IsValid(modeComp)) return;
            var iCombat = modeComp.CombatMode;
            if (!IsValid(iCombat)) return;
            var combatMode = new CombatMode(iCombat.Pointer);

            //SkillTreePlugin.ModLog.LogInfo("[AutoRelicCombine] 背包打开，扫描重复秘宝...");
            TryAutoCombineAll(combatMode);
        }

        // 拾取物品时触发扫描
        [HarmonyPatch(typeof(CombatMode), nameof(CombatMode.PickupItemLogic))]
        [HarmonyPostfix]
        public static void PickupItemLogic_Postfix(CombatMode __instance)
        {
            TryAutoCombineAll(__instance);
        }

        // 丢弃/掉落物品时触发扫描
        [HarmonyPatch(typeof(CombatMode), nameof(CombatMode.DropItem), new Type[] { typeof(ItemData), typeof(Vector3) })]
        [HarmonyPostfix]
        public static void DropItem_Postfix(CombatMode __instance)
        {
            TryAutoCombineAll(__instance);
        }

        // 服务器同步秘宝数据时触发扫描
        [HarmonyPatch(typeof(CombatMode), nameof(CombatMode.RefreshRelic), new Type[] { typeof(Il2CppSystem.Collections.Generic.List<RelicOnlineData>), typeof(Il2CppSystem.Collections.Generic.List<RelicOnlineData>) })]
        [HarmonyPostfix]
        public static void RefreshRelicAll_Postfix(CombatMode __instance)
        {
            TryAutoCombineAll(__instance);
        }

        [HarmonyPatch(typeof(CombatMode), nameof(CombatMode.RefreshRelic), new Type[] { typeof(RelicOnlineData) })]
        [HarmonyPostfix]
        public static void RefreshRelicSingle_Postfix(CombatMode __instance)
        {
            TryAutoCombineAll(__instance);
        }

        // 地图切换时清空已处理记录
        [HarmonyPatch(typeof(CombatMode), nameof(CombatMode.CrossMap))]
        [HarmonyPostfix]
        public static void CrossMap_Postfix()
        {
            _processedPairs.Clear();
        }

        #endregion

        #region Core Logic

        // 从RelicMode获取秘宝数据并扫描合成
        public static void TryAutoCombineAll(CombatMode combatMode)
        {
            if (_isProcessing) return;

            // 获取 RelicMode
            var modeComp = GameEntry.Mode;
            if (!IsValid(modeComp))
            {
                SkillTreePlugin.ModLog.LogWarning("[AutoRelicCombine] GameEntry.Mode 无效");
                return;
            }
            var relicMode = modeComp.RelicMode;
            if (!IsValid(relicMode))
            {
                SkillTreePlugin.ModLog.LogWarning("[AutoRelicCombine] RelicMode 无效");
                return;
            }

            // 确保有CombatMode用于调用CombineRelic
            CombatMode cm = combatMode;
            if (!IsValid(cm))
            {
                var iCombat = modeComp.CombatMode;
                if (IsValid(iCombat))
                    cm = new CombatMode(iCombat.Pointer);
            }
            if (!IsValid(cm))
            {
                SkillTreePlugin.ModLog.LogWarning("[AutoRelicCombine] 无法获取CombatMode");
                return;
            }

            try
            {
                _isProcessing = true;
                TryAutoCombineFromRelicMode(cm, relicMode);
            }
            catch (Exception ex)
            {
                SkillTreePlugin.ModLog.LogWarning("[AutoRelicCombine] 扫描异常: " + ex.Message + "\n" + ex.StackTrace);
            }
            finally
            {
                _isProcessing = false;
            }
        }

        // 从RelicMode读取玩家秘宝列表，检测重复并合成
        private static void TryAutoCombineFromRelicMode(CombatMode combatMode, IRelicMode relicMode)
        {
            // 获取玩家拥有的秘宝列表
            var ownRelicList = relicMode.GetOwnRelicList();
            if (!IsValid(ownRelicList))
            {
                SkillTreePlugin.ModLog.LogWarning("[AutoRelicCombine] GetOwnRelicList 返回 null");
                return;
            }

            int totalCount = ownRelicList.Count;
            //SkillTreePlugin.ModLog.LogInfo("[AutoRelicCombine] RelicMode秘宝总数=" + totalCount);

            if (totalCount < 2)
            {
                //SkillTreePlugin.ModLog.LogInfo("[AutoRelicCombine] 秘宝不足2个，跳过");
                return;
            }

            // 输出所有秘宝ID
            var idList = new List<string>();
            for (int i = 0; i < totalCount; i++)
            {
                var ri = ownRelicList[i];
                if (IsValid(ri) && IsValid(ri.RelicData))
                    idList.Add("[" + ri.RelicData.ID + "]#" + ri.RelicData.KeyID);
            }
            //SkillTreePlugin.ModLog.LogInfo("[AutoRelicCombine] 秘宝列表: " + string.Join(", ", idList.ToArray()));

            // 按 RelicData.ID 分组
            var groups = new Dictionary<int, List<RelicInfo>>();
            for (int i = 0; i < totalCount; i++)
            {
                var ri = ownRelicList[i];
                if (!IsValid(ri) || !IsValid(ri.RelicData)) continue;

                int itemId = ri.RelicData.ID;
                if (!groups.ContainsKey(itemId))
                    groups[itemId] = new List<RelicInfo>();
                groups[itemId].Add(ri);
            }

            // 输出分组
            //foreach (var kvp in groups) SkillTreePlugin.ModLog.LogInfo("[AutoRelicCombine]   ID=" + kvp.Key + " 数量=" + kvp.Value.Count);

            // 处理重复组
            foreach (var kvp in groups)
            {
                int itemId = kvp.Key;
                var relics = kvp.Value;

                while (relics.Count >= 2)
                {
                    var r1 = relics[0];
                    var r2 = relics[1];
                    relics.RemoveAt(0);
                    relics.RemoveAt(0);

                    if (!IsValid(r1) || !IsValid(r1.RelicData)) continue;
                    if (!IsValid(r2) || !IsValid(r2.RelicData)) continue;

                    string pairKey = r1.RelicData.KeyID + "_" + r2.RelicData.KeyID;
                    if (_processedPairs.Contains(pairKey))
                    {
                        SkillTreePlugin.ModLog.LogInfo("[AutoRelicCombine] 跳过已处理: " + pairKey);
                        continue;
                    }

                    //SkillTreePlugin.ModLog.LogInfo("[AutoRelicCombine] 尝试合成: ID=" + itemId + " x2 (Key=" + r1.RelicData.KeyID + "," + r2.RelicData.KeyID + ")");

                    bool forged = DoCombineById(combatMode, relicMode, itemId, r1, r2);
                    _processedPairs.Add(pairKey);

                    //SkillTreePlugin.ModLog.LogInfo("[AutoRelicCombine] 合成结果: " + (forged ? "成功" : "失败") + " ID=" + itemId);
                }
            }
        }

        // 按秘宝物品ID执行合成：移除2个基础秘宝，添加1个高级秘宝
        private static bool DoCombineById(CombatMode combatMode, IRelicMode relicMode, int itemId, RelicInfo r1, RelicInfo r2)
        {
            try
            {
                // 查询合成结果ID: GetRelicCombineID(源ID, 源ID) = 结果ID
                int forgedId = relicMode.GetRelicCombineID(itemId, itemId);
                //SkillTreePlugin.ModLog.LogInfo("[AutoRelicCombine] GetRelicCombineID(" + itemId + "," + itemId + ")=" + forgedId);

                if (forgedId <= 0)
                {
                    SkillTreePlugin.ModLog.LogWarning("[AutoRelicCombine] 无法获取合成结果ID");
                    return false;
                }

                // 移除基础秘宝
                relicMode.RemoveRelic(r1, true, true, true);
                relicMode.RemoveRelic(r2, true, true, true);
                //SkillTreePlugin.ModLog.LogInfo("[AutoRelicCombine] 已移除2个 ID=" + itemId);

                // 创建并添加合成结果
                var forgedRelic = relicMode.CreateRelicDataByID(forgedId);
                if (IsValid(forgedRelic))
                {
                    relicMode.AddRelic(forgedRelic);
                    //SkillTreePlugin.ModLog.LogInfo("[AutoRelicCombine] 已添加合成结果 ID=" + forgedId);
                }
                else
                {
                    SkillTreePlugin.ModLog.LogWarning("[AutoRelicCombine] CreateRelicDataByID(" + forgedId + ") 失败");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                SkillTreePlugin.ModLog.LogWarning("[AutoRelicCombine] DoCombineById异常: " + ex.Message + "\n" + ex.StackTrace);
                return false;
            }
        }

        #endregion
    }
}
