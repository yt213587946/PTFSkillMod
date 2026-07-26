using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Wogame;
using Wogame.Combat;

namespace PTFSkillMod
{
    // 每击败一个Boss，后续Boss血量 = 原HP × (1 + 击败数×2)
    public static class BossHPScalingPatch
    {
        private static int _bossesDefeated = 0;
        private static string _savePath;

        public static int BossesDefeatedCount => _bossesDefeated;

        private static string SavePath
        {
            get
            {
                if (_savePath == null)
                {
                    string dir = Path.Combine(BepInEx.Paths.ConfigPath, "com.violet.mod.skilltree3to2");
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    _savePath = Path.Combine(dir, "bosscount.txt");
                }
                return _savePath;
            }
        }

        public static void LoadBossCount()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    string content = File.ReadAllText(SavePath).Trim();
                    if (int.TryParse(content, out int count) && count >= 0)
                        _bossesDefeated = count;
                }
            }
            catch (Exception ex)
            {
                SkillTreePlugin.ModLog.LogWarning("[BossHP] 加载Boss计数失败: " + ex.Message);
            }
        }

        private static void SaveBossCount()
        {
            try
            {
                File.WriteAllText(SavePath, _bossesDefeated.ToString());
            }
            catch (Exception ex)
            {
                SkillTreePlugin.ModLog.LogWarning("[BossHP] 保存Boss计数失败: " + ex.Message);
            }
        }

        private static bool IsInstanceValid(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase instance)
        {
            return instance != null && instance.Pointer != IntPtr.Zero;
        }

        // 监听 Boss 死亡事件，累加击败数
        [HarmonyPatch(typeof(BattleForm), nameof(BattleForm.BossHpBarDead))]
        [HarmonyPostfix]
        public static void BossHpBarDead_Postfix()
        {
            _bossesDefeated++;
            SaveBossCount();
            //SkillTreePlugin.ModLog.LogInfo("[BossHP] Boss击败! 当前累计击败数=" + _bossesDefeated + " 下一个Boss倍率=" + (1L + _bossesDefeated * 2) + "x");
        }


        // 修改 Boss 基础血量
        [HarmonyPatch(typeof(CombatEntity), nameof(CombatEntity.InitCombatAttr))]
        [HarmonyPostfix]
        public static void InitCombatAttr_Postfix(CombatEntity __instance)
        {
            if (!IsInstanceValid(__instance)) return;
            if (_bossesDefeated <= 0) return;
            if (__instance.UnitType != UnitType.Boss) return;

            try
            {
                long mult = 1L + _bossesDefeated * 2;
                var maxHpAttr = __instance.GetMyAttribute(AttributeType.MaxHealthPoint);
                var hpAttr = __instance.GetMyAttribute(AttributeType.HealthPoint);

                if (!IsInstanceValid(maxHpAttr) || !IsInstanceValid(hpAttr)) return;

                long originalMax = maxHpAttr.BaseValue;
                long newMax = originalMax * mult;

                maxHpAttr.SetBase(newMax);
                hpAttr.SetBase(newMax);

                string cfgName = __instance.ConfigName ?? "";

                SkillTreePlugin.ModLog.LogInfo("[BossHP] Boss登场 cfgId=" + __instance.ConfigId + " 名称=" + (__instance.ConfigName ?? "?")
                    + " 原始HP=" + originalMax + " → 实际HP=" + newMax + " (" + mult + "x)");
            }
            catch (Exception ex)
            {
                SkillTreePlugin.ModLog.LogWarning("[BossHP] 修改HP异常: " + ex.GetType().Name + " " + ex.Message);
            }
        }
    }
}
