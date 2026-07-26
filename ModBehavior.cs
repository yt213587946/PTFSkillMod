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
        public const string VERSION = "1.1.0";

        public static ManualLogSource ModLog;

        public override void Load()
        {
            ModLog = base.Log;
            ModLog.LogInfo("========== 专精3选2Mod 正在加载... ==========");

            try
            {
                Harmony harmony = new Harmony(GUID);
                harmony.PatchAll(typeof(RoleSelectFormPatch));
                harmony.PatchAll(typeof(RoleSkillItemPatch));
                harmony.PatchAll(typeof(RoleModeDataPatch));
                harmony.PatchAll(typeof(AutoRelicCombinePatch));
                harmony.PatchAll(typeof(BossHPScalingPatch));
                harmony.PatchAll(typeof(PotionDropPatch));
                RoleSelectFormPatch.LoadFromFile();
                BossHPScalingPatch.LoadBossCount();
                ModLog.LogInfo(" SUCCESS: 所有 Harmony 补丁注入成功！专精数据已加载。");
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
        // 记录某一层是否已经被玩家手动点击或同步过
        public static readonly HashSet<int> InitializedLayers = new HashSet<int>();
        // <RoleID, <Layer, List<SkillID>>>
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

        // 从文件加载专精选择数据
        // 格式: ROLE_ID:LAYER=SKILL1,SKILL2,...
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

                    // 格式: ROLE_ID:LAYER=SKILL1,SKILL2,...
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

        // 保存专精选择数据到文件
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

        // 计算某一层所需的角色等级
        private static int GetRequiredLevelForLayer(int layer)
        {
            return layer  + 1;
        }

        // 获取当前角色在指定 Layer 的已选技能列表
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

        // 检查某个技能是否已被该角色激活
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
            //SkillTreePlugin.ModLog.LogInfo("[Mod] 打开角色专精界面，初始化专精管理器。");
        }

        [HarmonyPatch(typeof(RoleSelectForm), nameof(RoleSelectForm.UnlockSkill))]
        [HarmonyPrefix]
        public static bool UnlockSkill_Prefix(RoleSelectForm __instance)
        {
            if (!IsInstanceValid(__instance)) return true;

            int currentLayer = __instance.selectLayer;
            int currentSkillId = __instance.selectSkillID;

            if (currentSkillId <= 0) return true;

            // 标记玩家已对该层进行过手动操作
            InitializedLayers.Add(currentLayer);

            // 等级门槛校验
            int reqLevel = GetRequiredLevelForLayer(currentLayer);
            // 获取当前角色 ID
            int roleId = __instance.newRoleID > 0 ? __instance.newRoleID : __instance.oldHeroID;
            List<int> layerList = GetLayerSkillList(roleId, currentLayer);

            // 如果存在 RoleMode 数据组件，校验当前角色等级
            try
            {
                // 如果当前层所需的等级高于角色等级，阻止点亮
            }
            catch { }

            // 点击已激活的专精 ->取消激活
            if (layerList.Contains(currentSkillId))
            {
                layerList.Remove(currentSkillId);

                __instance.RefreshRoleInfo();
                return false; // 跳过原版解锁，直接完成取消激活
            }

            // 限制同层最多 2 个
            if (layerList.Count >= 2)
            {
                __instance.RefreshRoleInfo();
                return false;
            }

            layerList.Add(currentSkillId);
            UnlockedSkillIDs.Add(currentSkillId);

            return true;
        }

        [HarmonyPatch(typeof(RoleSelectForm), nameof(RoleSelectForm.UnlockSkill))]
        [HarmonyPostfix]
        public static void UnlockSkill_Postfix(RoleSelectForm __instance)
        {
            if (!IsInstanceValid(__instance)) return;

            try
            {
                __instance.RefreshRoleInfo();
            }
            catch { }

            SaveToFile();
        }
    }

    // 只对匹配到专精 ID 的图标亮黄框
    public static class RoleSkillItemPatch
    {
        private static bool IsInstanceValid(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase instance)
        {
            return instance != null && instance.Pointer != IntPtr.Zero;
        }

        [HarmonyPatch(typeof(RoleSkillItem), nameof(RoleSkillItem.RefreshItemState))]
        [HarmonyPrefix]
        public static void RefreshItemState_Prefix(RoleSkillItem __instance, ref SkillState state)
        {
            if (!IsInstanceValid(__instance)) return;

            int itemId = __instance.id;
            int itemLayer = __instance.layer;
            int roleId = __instance.roleID;
            List<int> layerList = RoleSelectFormPatch.GetLayerSkillList(roleId, itemLayer);

            // 只有该层完全没被手动操作过，且 layerList 为空时才载入原版技能
            if (state == SkillState.Study && layerList.Count == 0 && !RoleSelectFormPatch.InitializedLayers.Contains(itemLayer))
            {
                layerList.Add(itemId);
                RoleSelectFormPatch.InitializedLayers.Add(itemLayer);
            }

            // 只有在玩家激活的列表里的技能才设为 Study 亮起
            if (layerList.Contains(itemId))
            {
                state = SkillState.Study;
            }
            else
            {
                // 未选的技能保持未激活
                state = SkillState.UnActive;
            }
        }

        [HarmonyPatch(typeof(RoleSkillItem), nameof(RoleSkillItem.RefreshItemState))]
        [HarmonyPostfix]
        public static void RefreshItemState_Postfix(RoleSkillItem __instance, SkillState state)
        {
            if (!IsInstanceValid(__instance)) return;

            int roleId = __instance.roleID;
            int itemId = __instance.id;

            // 如果是激活的专精，强刷为全彩图标与亮黄框
            if (RoleSelectFormPatch.IsSkillUnlocked(roleId, itemId))
            {
                // 强行把图标从灰色 Shader 切回原彩 Shader (material = null)
                if (__instance.mImg_Icon != null && __instance.mImg_Icon.Pointer != IntPtr.Zero)
                {
                    __instance.mImg_Icon.material = null;
                    __instance.mImg_Icon.color = Color.white; // 恢复全彩不透明
                }

                // 强行显示“已激活”黄框
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
        private static bool IsInstanceValid(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase instance)
        {
            return instance != null && instance.Pointer != IntPtr.Zero;
        }

        [HarmonyPatch(typeof(RoleMode), nameof(RoleMode.GetStudyRoleSkillID))]
        [HarmonyPostfix]
        public static void GetStudyRoleSkillID_Postfix(RoleMode __instance,string roleID,
            ref Il2CppSystem.Collections.Generic.List<Il2CppStructArray<int>> __result)
        {
            if (!IsInstanceValid(__instance) || __result == null) return;

            int parsedRoleId = 0;
            int.TryParse(roleID, out parsedRoleId);

            if (RoleSelectFormPatch.SavedRoleLayerSkills.TryGetValue(parsedRoleId, out var layerDict))
            {
                foreach (var kvp in layerDict)
                {
                    int layer = kvp.Key;
                    List<int> chosenSkillIds = kvp.Value;

                    if (chosenSkillIds == null) continue;

                    // 转换 0-Based 索引
                    int targetIndex = (layer >= 1) ? (layer - 1) : layer;

                    if (targetIndex >= 0 && targetIndex < __result.Count)
                    {
                        int count = chosenSkillIds.Count;
                        Il2CppStructArray<int> newArray = new Il2CppStructArray<int>(count);

                        for (int i = 0; i < count; i++)
                        {
                            newArray[i] = chosenSkillIds[i];
                        }

                        // 将最新的列表写入底层
                        __result[targetIndex] = newArray;
                    }
                }
            }
        }
    }
}