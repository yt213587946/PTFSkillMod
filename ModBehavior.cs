using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using Wogame;

namespace PTFSkillMod
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class SkillTreePlugin : BasePlugin
    {
        public const string GUID = "com.violet.mod.skilltree3to2";
        public const string NAME = "专精3选2Mod";
        public const string VERSION = "1.1.1";

        public static ManualLogSource ModLog;

        public override void Load()
        {
            ModLog = base.Log;
            ModLog.LogInfo("========== 专精3选2Mod 正在加载... ==========");

            try
            {
                Harmony harmony = new Harmony(GUID);

                // ===== 仅启用功能 3 与 4 =====
                harmony.PatchAll(typeof(AutoRelicCombinePatch)); // 3. 自动秘宝合成
                harmony.PatchAll(typeof(PotionDropPatch));       // 4. Boss掉落药水

                // ===== 以下功能已禁用（按需取消注释即可开启）=====
                // harmony.PatchAll(typeof(RoleSelectFormPatch)); // 1. 专精3选2
                // harmony.PatchAll(typeof(RoleSkillItemPatch));
                // harmony.PatchAll(typeof(RoleModeDataPatch));
                // harmony.PatchAll(typeof(BossHPScalingPatch));  // 2. Boss血量递增
                // RoleSelectFormPatch.LoadFromFile();
                // BossHPScalingPatch.LoadBossCount();

                ModLog.LogInfo(" SUCCESS: Harmony 补丁注入成功（仅功能 3/4）。");
            }
            catch (Exception ex)
            {
                ModLog.LogError(" ERROR: 补丁注入失败！原因: " + ex.Message);
            }
        }
    }

    // 状态切换
    public static class RoleSelectFormPatch
    {
        public static readonly HashSet<int> UnlockedSkillIDs = new HashSet<int>();
        public static readonly Dictionary<int, List<int>> LayerSkillsMap = new Dictionary<int, List<int>>();
        public static readonly HashSet<int> InitializedLayers = new HashSet<int>();
        public static readonly Dictionary<int, Dictionary<int, List<int>>> SavedRoleLayerSkills = new Dictionary<int, Dictionary<int, List<int>>>();

        private static string _savePath;
        private static string SavePath
        {
            get
            {
                if (_savePath == null)
                {
                    string configDir = Path.Combine(Paths.ConfigPath, "com.violet.mod.skilltree3to2");
                    if (!Directory.Exists(configDir))
                        Directory.CreateDirectory(configDir);
                    _savePath = Path.Combine(configDir, "skilldata.txt");
                }
                return _savePath;
            }
        }

        private static bool IsInstanceValid(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase instance)
        {
            return instance != null && instance.Pointer != IntPtr.Zero;
        }

        public static void LoadFromFile()
        {
            try
            {
                string path = SavePath;
                if (!File.Exists(path))
                {
                    SkillTreePlugin.ModLog.LogInfo("[SkillPersist] 存档文件不存在，使用默认数据。");
                    return;
                }

                SavedRoleLayerSkills.Clear();
                InitializedLayers.Clear();
                UnlockedSkillIDs.Clear();

                string[] lines = File.ReadAllLines(path);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith("#")) continue;

                    int colonPos = trimmed.IndexOf(':');
                    int equalPos = trimmed.IndexOf('=');
                    if (colonPos < 0 || equalPos < 0 || colonPos >= equalPos) continue;

                    string roleStr = trimmed.Substring(0, colonPos);
                    string layerStr = trimmed.Substring(colonPos + 1, equalPos - colonPos - 1);
                    string skillsStr = trimmed.Substring(equalPos + 1);

                    if (!int.TryParse(roleStr, out int roleId)) continue;
                    if (!int.TryParse(layerStr, out int layer)) continue;

                    List<int> skillList = GetLayerSkillList(roleId, layer);
                    skillList.Clear();

                    if (skillsStr.Length > 0)
                    {
                        string[] skillParts = skillsStr.Split(',');
                        foreach (string s in skillParts)
                        {
                            if (int.TryParse(s.Trim(), out int skillId) && skillId > 0)
                            {
                                skillList.Add(skillId);
                                UnlockedSkillIDs.Add(skillId);
                            }
                        }
                    }
                }

                SkillTreePlugin.ModLog.LogInfo("[SkillPersist] 专精数据加载完成，已恢复 " + SavedRoleLayerSkills.Count + " 个角色的技能选择。");
            }
            catch (Exception ex)
            {
                SkillTreePlugin.ModLog.LogWarning("[SkillPersist] 加载失败: " + ex.Message);
            }
        }

        public static void SaveToFile()
        {
            try
            {
                var lines = new List<string>();
                lines.Add("# BinDun SkillTreeMod Save Data v1");
                lines.Add("# Format: ROLE_ID:LAYER=SKILL1,SKILL2,...");

                foreach (var roleKvp in SavedRoleLayerSkills)
                {
                    int roleId = roleKvp.Key;
                    foreach (var layerKvp in roleKvp.Value)
                    {
                        int layer = layerKvp.Key;
                        var skills = layerKvp.Value;
                        if (skills == null || skills.Count == 0) continue;
                        string skillStr = string.Join(",", skills);
                        lines.Add(roleId + ":" + layer + "=" + skillStr);
                    }
                }

                File.WriteAllLines(SavePath, lines.ToArray());
            }
            catch (Exception ex)
            {
                SkillTreePlugin.ModLog.LogWarning("[SkillPersist] 保存失败: " + ex.Message);
            }
        }

        private static int GetRequiredLevelForLayer(int layer)
        {
            return layer + 1;
        }

        public static List<int> GetLayerSkillList(int roleId, int layer)
        {
            if (!SavedRoleLayerSkills.ContainsKey(roleId))
            {
                SavedRoleLayerSkills[roleId] = new Dictionary<int, List<int>>();
            }
            if (!SavedRoleLayerSkills[roleId].ContainsKey(layer))
            {
                SavedRoleLayerSkills[roleId][layer] = new List<int>();
            }
            return SavedRoleLayerSkills[roleId][layer];
        }

        public static bool IsSkillUnlocked(int roleId, int skillId)
        {
            if (SavedRoleLayerSkills.TryGetValue(roleId, out var layerDict))
            {
                foreach (var list in layerDict.Values)
                {
                    if (list != null && list.Contains(skillId)) return true;
                }
            }
            return false;
        }

        [HarmonyPatch(typeof(RoleSelectForm), nameof(RoleSelectForm.OnOpen))]
        [HarmonyPostfix]
        public static void OnOpen_Postfix(RoleSelectForm __instance)
        {
            if (!IsInstanceValid(__instance)) return;
        }

        [HarmonyPatch(typeof(RoleSelectForm), nameof(RoleSelectForm.UnlockSkill))]
        [HarmonyPrefix]
        public static bool UnlockSkill_Prefix(RoleSelectForm __instance)
        {
            if (!IsInstanceValid(__instance)) return true;

            int currentLayer = __instance.selectLayer;
            int currentSkillId = __instance.selectSkillID;

            if (currentSkillId <= 0) return true;

            InitializedLayers.Add(currentLayer);

            int roleId = __instance.newRoleID > 0 ? __instance.newRoleID : __instance.oldHeroID;
            List<int> layerList = GetLayerSkillList(roleId, currentLayer);

            //SkillTreePlugin.ModLog.LogInfo("[SkillDebug] roleId=" + roleId + " layer=" + currentLayer + " skillId=" + currentSkillId + " count=" + layerList.Count);

            // 点击已激活的专精 ->取消激活
            if (layerList.Contains(currentSkillId))
            {
               layerList.Remove(currentSkillId);
                __instance.RefreshRoleInfo();
               SaveToFile();
                return false;
            }

            // 限制同层最多 2 个
            if (layerList.Count >= 2)
            {
                __instance.RefreshRoleInfo();
                return false;
            }

            layerList.Add(currentSkillId);
            UnlockedSkillIDs.Add(currentSkillId);
            SaveToFile();

            return false;
        }

        [HarmonyPatch(typeof(RoleSelectForm), nameof(RoleSelectForm.UnlockSkill))]
        [HarmonyPostfix]
        public static void UnlockSkill_Postfix(RoleSelectForm __instance)
        {
            if (!IsInstanceValid(__instance)) return;
            SaveToFile();
        }
    }

   public static class RoleSkillItemPatch
    {
        private static bool IsInstanceValid(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase instance)
        {
            return instance != null && instance.Pointer != IntPtr.Zero;
        }

        [HarmonyPatch(typeof(RoleSkillItem), nameof(RoleSkillItem.RefreshItemState))]
        [HarmonyPrefix]
        public static bool RefreshItemState_Prefix(RoleSkillItem __instance, ref SkillState state)
        {
            if (!IsInstanceValid(__instance)) return true;
            try
            {
                int itemId = __instance.id;
                int itemLayer = __instance.layer;
                int roleId = __instance.roleID;
                List<int> layerList = RoleSelectFormPatch.GetLayerSkillList(roleId, itemLayer);

                if (state == SkillState.Study && layerList.Count == 0 && !RoleSelectFormPatch.InitializedLayers.Contains(itemLayer))
                {
                    layerList.Add(itemId);
                    RoleSelectFormPatch.InitializedLayers.Add(itemLayer);
                }

                if (layerList.Contains(itemId))
                    state = SkillState.Study;
                else
                    state = SkillState.UnActive;
                return true; // 让原方法执行，确保内部状态同步
            }
           catch { return true; }
       }
        [HarmonyPatch(typeof(RoleSkillItem), nameof(RoleSkillItem.RefreshItemState))]
        [HarmonyPostfix]
        public static void RefreshItemState_Postfix(RoleSkillItem __instance, SkillState state)
        {
            if (!IsInstanceValid(__instance)) return;
            int roleId = __instance.roleID;
            int itemId = __instance.id;
            if (RoleSelectFormPatch.IsSkillUnlocked(roleId, itemId))
            {
                if (__instance.mImg_Study != null && __instance.mImg_Study.Pointer != IntPtr.Zero)
                {
                    __instance.mImg_Study.gameObject.SetActive(true);
                }
            }
        }

       [HarmonyPatch(typeof(RoleSkillItem), nameof(RoleSkillItem.SetSelect))]
        [HarmonyPostfix]
        public static void SetSelect_Postfix(RoleSkillItem __instance)
        {
            if (!IsInstanceValid(__instance)) return;

            int roleId = __instance.roleID;
            int itemId = __instance.id;

            if (RoleSelectFormPatch.IsSkillUnlocked(roleId, itemId))
            {
                if (__instance.mImg_Icon != null && __instance.mImg_Icon.Pointer != IntPtr.Zero)
                {
                    __instance.mImg_Icon.material = null;
                    __instance.mImg_Icon.color = Color.white;
                }
                if (__instance.mImg_Study != null && __instance.mImg_Study.Pointer != IntPtr.Zero)
                {
                    __instance.mImg_Study.gameObject.SetActive(true);
                }
            }
        }
    }

    // 向系统写入专精列表
    public static class RoleModeDataPatch
    {
        // 拦截 GetLayerSkillState 防止因数组长度不匹配导致 IndexOutOfRangeException
        [HarmonyPatch(typeof(RoleMode), nameof(RoleMode.GetLayerSkillState))]
        [HarmonyPrefix]
        public static bool GetLayerSkillState_Prefix(RoleMode __instance, int roleID, int layer, ref SkillState __result)
        {
            __result = SkillState.Study;
            return false;
        }

        private static bool IsInstanceValid(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase instance)
        {
            return instance != null && instance.Pointer != IntPtr.Zero;
        }

        [HarmonyPatch(typeof(RoleMode), nameof(RoleMode.GetStudyRoleSkillID))]
        [HarmonyPostfix]
        public static void GetStudyRoleSkillID_Postfix(RoleMode __instance, string roleID,
            ref Il2CppSystem.Collections.Generic.List<Il2CppStructArray<int>> __result)
        {
            if (!IsInstanceValid(__instance) || __result == null) return;

            try
            {
                int parsedRoleId = 0;
                int.TryParse(roleID, out parsedRoleId);

                if (RoleSelectFormPatch.SavedRoleLayerSkills.TryGetValue(parsedRoleId, out var layerDict))
                {
                    foreach (var kvp in layerDict)
                    {
                        int layer = kvp.Key;
                        List<int> chosenSkillIds = kvp.Value;
                        if (chosenSkillIds == null) continue;

                        int targetIndex = (layer >= 1) ? (layer - 1) : layer;
                        if (targetIndex < 0 || targetIndex >= __result.Count) continue;

                        int count = chosenSkillIds.Count;
                        Il2CppStructArray<int> newArray = new Il2CppStructArray<int>(count);
                        for (int i = 0; i < count; i++)
                            newArray[i] = chosenSkillIds[i];

                        __result[targetIndex] = newArray;
                    }
                }
            }
            catch { }
        }
    }
}
