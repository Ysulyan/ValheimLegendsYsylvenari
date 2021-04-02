﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System.IO;
using UnityEngine.UI;

namespace ValheimLegends
{
    [BepInPlugin("ValheimLegends", "ValheimLegends", "0.2.1")]
    [BepInProcess("valheim.exe")]
    public class ValheimLegends : BaseUnityPlugin
    {
        //configs
        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<string> Ability1_Hotkey;
        public static ConfigEntry<string> Ability1_Hotkey_Combo;
        public static ConfigEntry<string> Ability2_Hotkey;
        public static ConfigEntry<string> Ability2_Hotkey_Combo;
        public static ConfigEntry<string> Ability3_Hotkey;
        public static ConfigEntry<string> Ability3_Hotkey_Combo;

        public static ConfigEntry<float> energyCostMultiplier;
        public static ConfigEntry<float> cooldownMultiplier;
        public static ConfigEntry<float> abilityDamageMultiplier;
        public static ConfigEntry<float> skillGainMultiplier;

        public static ConfigEntry<string> chosenClass;
        public static readonly Color abilityCooldownColor = new Color(1f, .3f, .3f, .5f);

        public static ConfigEntry<bool> showAbilityIcons;
        public static ConfigEntry<string> iconAlignment;

        public static bool ClassIsValid
        {
            get
            {
                if (chosenClass.Value?.ToString() == "Valkyrie" ||
                chosenClass.Value?.ToString() == "Mage" ||
                chosenClass.Value?.ToString() == "Berserker" ||
                chosenClass.Value?.ToString() == "Druid" ||
                chosenClass.Value?.ToString() == "Ranger" ||
                chosenClass.Value?.ToString() == "Shaman")
                {
                    return true;
                }
                return false;
            }
        }

        private static readonly Type patchType = typeof(ValheimLegends);

        //objects
        public static Sprite Ability1_Sprite;
        public static Sprite Ability2_Sprite;
        public static Sprite Ability3_Sprite;

        public static string Ability1_Name;
        public static string Ability2_Name;
        public static string Ability3_Name;

        public static List<RectTransform> abilitiesStatus = new List<RectTransform>();

        //global variables
        public static bool shouldUseGuardianPower = true;
        public static bool shouldValkyrieImpact = false;
        public static bool isChanneling = false;
        public static int channelingCancelDelay = 0;
        public static bool isChargingDash = false;
        public static int dashCounter = 0;

        //Skills
        public static readonly int DisciplineSkillID = 781;
        public static readonly int AbjurationSkillID = 791;
        public static readonly int AlterationSkillID = 792;                
        public static readonly int ConjurationSkillID = 793;
        public static readonly int EvocationSkillID = 794;


        public static Skills.SkillType DisciplineSkill = (Skills.SkillType)DisciplineSkillID;
        public static Skills.SkillType AbjurationSkill = (Skills.SkillType)AbjurationSkillID;
        public static Skills.SkillType AlterationSkill = (Skills.SkillType)AlterationSkillID;
        public static Skills.SkillType ConjurationSkill = (Skills.SkillType)ConjurationSkillID;
        public static Skills.SkillType EvocationSkill = (Skills.SkillType)EvocationSkillID;        

        public static Skills.SkillDef DisciplineSkillDef;
        public static Skills.SkillDef AbjurationSkillDef;
        public static Skills.SkillDef AlterationSkillDef;
        public static Skills.SkillDef ConjurationSkillDef;
        public static Skills.SkillDef EvocationSkillDef;

        public static List<Skills.SkillDef> legendsSkills = new List<Skills.SkillDef>();

        //informational patches
        //[HarmonyPatch(typeof(StatusEffect), "Setup", null)]
        //public class MonitorStatusEffects_Patch
        //{
        //    public static void Postfix(StatusEffect __instance, string ___m_name, string ___m_category)
        //    {
        //        ZLog.Log("Setup status: (" + ___m_name + ") category: (" + ___m_category + ")");
        //    }
        //}

        //[HarmonyPatch(typeof(ZSyncAnimation), "RPC_SetTrigger", null)]
        //public class AnimationTrigger_Monitor_Patch
        //{
        //    public static void Postfix(ZSyncAnimation __instance, long sender, string name)
        //    {
        //        ZLog.Log("animation: " + name);
        //    }
        //}

        //
        //mod patches
        //

        [HarmonyPatch(typeof(Player), "ActivateGuardianPower", null)]
        public class ActivatePowerPrevention_Patch
        {
            public static bool Prefix(Player __instance, ref bool __result)
            {                
                if (!shouldUseGuardianPower)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Player), "OnDodgeMortal", null)]
        public class DodgeBreaksChanneling_Patch
        {
            public static void Postfix(Player __instance)
            {
                if (isChanneling)
                {
                    ValheimLegends.isChanneling = false;
                }
            }
        }

        [HarmonyPatch(typeof(Player), "StartGuardianPower", null)]
        public class StartPowerPrevention_Patch
        {
            public static bool Prefix(Player __instance, ref bool __result)
            {
                if (!shouldUseGuardianPower)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Player), "CanMove", null)]
        public class CanMove_Casting_Patch
        {
            public static void Postfix(Player __instance, ref bool __result)
            {
                if(isChanneling)
                {
                    __result = false;
                }
            }
        }

        [HarmonyPatch(typeof(Menu), "OnQuit", null)]
        public class QuitYes_Patch
        {
            public static bool Prefix()
            {
                RemoveSummonedWolf();
                return true;
            }
        }

        [HarmonyPatch(typeof(Menu), "OnLogout", null)]
        public class RemoveWolfOnLogout_Patch
        {
            public static bool Prefix()
            {
                RemoveSummonedWolf();
                return true;
            }
        }

        private static void RemoveSummonedWolf()
        {
            foreach (Character ch in Character.GetAllCharacters())
            {
                if (ch != null && ch.GetSEMan() != null && ch.GetSEMan().HaveStatusEffect("SE_VL_Companion"))
                {
                    
                    SE_Companion se_c = ch.GetSEMan().GetStatusEffect("SE_VL_Companion") as SE_Companion;
                    if (se_c.summoner == Player.m_localPlayer)
                    {
                        ch.transform.SetPositionAndRotation(new Vector3(0, 0, 0), Quaternion.identity);
                        //Game.DestroyImmediate(ch.gameObject, true);
                        ch.m_faction = Character.Faction.MountainMonsters;
                        HitData hit = new HitData();
                        hit.m_damage.m_spirit = 9999f;
                        ch.Damage(hit);
                    }
                }
            }
        }


        [HarmonyPatch(typeof(BaseAI), "CanSenseTarget", new Type[]
        {
            typeof(Character)
        })]
        public class CanSee_Shadow_Patch
        {
            public static bool Prefix(BaseAI __instance, Character target, ref bool __result)
            {
                if (target != null)
                {
                    Player player = target as Player;
                    if (player != null && player.GetSEMan().HaveStatusEffect("SE_VL_ShadowStalk"))
                    {
                        if (player.IsCrouching())
                        {
                            __result = false;
                            return false;
                        }
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Character), "UpdateGroundContact", null)]
        public class Valkyrie_ValidateHeight_Patch
        {
            public static void Postfix(Character __instance, float ___m_maxAirAltitude, bool ___m_groundContact)
            {
                if (__instance == Player.m_localPlayer)
                {
                    if (Class_Valkyrie.inFlight)
                    {
                        if (Mathf.Max(0f, ___m_maxAirAltitude - __instance.transform.position.y) > 1f)
                        {
                            ValheimLegends.shouldValkyrieImpact = true;
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Character), "ResetGroundContact", null)]
        public class Valkyrie_GroundContact_Patch
        {
            public static void Postfix(Character __instance, float ___m_maxAirAltitude, bool ___m_groundContact)
            {
                if (__instance == Player.m_localPlayer && shouldValkyrieImpact)
                {
                    float maxAltitude = Mathf.Max(0f, ___m_maxAirAltitude - __instance.transform.position.y);
                    ValheimLegends.shouldValkyrieImpact = false;
                    Class_Valkyrie.Impact_Effect(Player.m_localPlayer, maxAltitude);
                    Class_Valkyrie.inFlight = false;
                    if (maxAltitude > 4f)
                    {
                        Class_Valkyrie.inFlight = true;
                    }

                }
            }
        }

        [HarmonyPatch(typeof(OfferingBowl), "UseItem", null)]
        public class OfferingForClass_Patch
        {
            public static bool Prefix(OfferingBowl __instance, Humanoid user, ItemDrop.ItemData item, Transform ___m_itemSpawnPoint, EffectList ___m_fuelAddedEffects)
            {
                //ZLog.Log("offered item is " + item.m_shared.m_name + " to string " + item.ToString());
                int num = user.GetInventory().CountItems(item.m_shared.m_name);
                bool flag = false;
                if (item.m_shared.m_name.Contains("item_greydwarfeye") && chosenClass.Value != "Shaman")
                {                    
                    user.Message(MessageHud.MessageType.Center, "Acquired the powers of a Shaman");
                    ValheimLegends.chosenClass.Value = "Shaman";
                    flag = true;
                }
                else if (item.m_shared.m_name.Contains("item_meat_raw") && chosenClass.Value != "Ranger")
                {
                    user.Message(MessageHud.MessageType.Center, "Acquired the powers of a Ranger");
                    ValheimLegends.chosenClass.Value = "Ranger";
                    flag = true;
                }
                else if (item.m_shared.m_name.Contains("item_coal") && chosenClass.Value != "Mage")
                {
                    user.Message(MessageHud.MessageType.Center, "Acquired the powers of a Mage");
                    ValheimLegends.chosenClass.Value = "Mage";
                    flag = true;
                }
                else if (item.m_shared.m_name.Contains("item_flint") && chosenClass.Value != "Valkyrie")
                {
                    user.Message(MessageHud.MessageType.Center, "Acquired the powers of a Valkyrie");
                    ValheimLegends.chosenClass.Value = "Valkyrie";
                    flag = true;
                }
                else if (item.m_shared.m_name.Contains("item_dandelion") && chosenClass.Value != "Druid")
                {
                    user.Message(MessageHud.MessageType.Center, "Acquired the powers of a Druid");
                    ValheimLegends.chosenClass.Value = "Druid";
                    flag = true;
                }
                else if (item.m_shared.m_name.Contains("item_bonefragments") && chosenClass.Value != "Berserker")
                {
                    user.Message(MessageHud.MessageType.Center, "Acquired the powers of a Berserker");
                    ValheimLegends.chosenClass.Value = "Berserker";
                    flag = true;
                }
                if (flag)
                {
                    user.GetInventory().RemoveItem(item.m_shared.m_name, 1);
                    user.ShowRemovedMessage(item, 1);
                    NameCooldowns();
                    if ((bool)___m_itemSpawnPoint && ___m_fuelAddedEffects != null)
                    {
                        ___m_fuelAddedEffects.Create(___m_itemSpawnPoint.position, __instance.transform.rotation);
                    }
                    UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("fx_GP_Activation"), user.GetCenterPoint(), Quaternion.identity);

                    foreach (RectTransform ability in ValheimLegends.abilitiesStatus)
                    {
                        UnityEngine.Object.Destroy(ability.gameObject);
                    }
                    ValheimLegends.abilitiesStatus.Clear();
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Character), "Damage", null)]
        public class VL_Damage_Patch
        {
            public static bool Prefix(Character __instance, ref HitData hit, float ___m_maxAirAltitude)
            {
                Character attacker = hit.GetAttacker();
                if (__instance == Player.m_localPlayer)
                {
                    if (Class_Valkyrie.inFlight)// && Mathf.Max(0f, ___m_maxAirAltitude - __instance.transform.position.y) > 4f)
                    {
                        Class_Valkyrie.inFlight = false;                        
                        return false;
                    }
                    if(__instance.GetSEMan().HaveStatusEffect("SE_VL_Bulwark"))
                    {
                        //ZLog.Log("has status effect SE_VL_Bulwark");
                        hit.m_damage.Modify(.75f - (Player.m_localPlayer.GetSkills().GetSkillList().FirstOrDefault((Skills.Skill x) => x.m_info == ValheimLegends.AbjurationSkillDef).m_level/10f));
                    }                    
                }

                if (attacker != null )
                {
                    //ZLog.Log("attacker is " + attacker.name);
                    if (attacker.GetSEMan().HaveStatusEffect("SE_VL_ShadowStalk"))
                    {
                        //ZLog.Log("removing shadowstalk status");
                        attacker.GetSEMan().RemoveStatusEffect("SE_VL_ShadowStalk", true);
                    }
                    if (attacker.GetSEMan().HaveStatusEffect("SE_VL_Shell"))
                    {
                        SE_Shell se_shell = attacker.GetSEMan().GetStatusEffect("SE_VL_Shell") as SE_Shell;
                        hit.m_damage.m_spirit += se_shell.spiritDamageOffset;
                    }
                    if(attacker.GetSEMan().HaveStatusEffect("SE_VL_Berserk"))
                    {
                        SE_Berserk se_berserk = attacker.GetSEMan().GetStatusEffect("SE_VL_Berserk") as SE_Berserk;
                        attacker.Heal(hit.GetTotalPhysicalDamage() * se_berserk.healthAbsorbPercent, true);
                    }
                    if (attacker.GetSEMan().HaveStatusEffect("SE_VL_Execute"))
                    {
                        SE_Execute se_berserk = attacker.GetSEMan().GetStatusEffect("SE_VL_Execute") as SE_Execute;
                        hit.m_staggerMultiplier *= se_berserk.staggerForce;
                        hit.m_damage.m_blunt *= se_berserk.damageBonus;
                        hit.m_damage.m_pierce *= se_berserk.damageBonus;
                        hit.m_damage.m_slash *= se_berserk.damageBonus;
                        se_berserk.hitCount--;
                        if(se_berserk.hitCount <= 0)
                        {
                            attacker.GetSEMan().RemoveStatusEffect(se_berserk, true);
                        }
                    }
                    if (attacker.GetSEMan().HaveStatusEffect("SE_VL_Companion"))
                    {
                        SE_Companion se_companion = attacker.GetSEMan().GetStatusEffect("SE_VL_Companion") as SE_Companion;
                        hit.m_damage.Modify(se_companion.damageModifier);
                    }
                }

                //hit.m_damage.Modify(.5f);
                //__instance.GetSEMan().HaveStatusEffect()

                return true;
            }
        }

        [HarmonyPatch(typeof(Humanoid), "BlockAttack", null)]
        public class Block_Bulwark_Patch
        {
            public static bool Prefix(Humanoid __instance, ref bool __result)
            {
                if (__instance == Player.m_localPlayer)
                {
                    if(__instance.GetSEMan().HaveStatusEffect("SE_VL_Bulwark"))
                    {
                        Class_Valkyrie.isBlocking = true;
                    }
                    else
                    {
                        Class_Valkyrie.isBlocking = false;
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Attack), "DoMeleeAttack", null)]
        public class MeleeAttack_Patch
        {
            public static bool Prefix(Attack __instance, Humanoid ___m_character, ref float ___m_damageMultiplier)
            {
                if (___m_character.GetSEMan().HaveStatusEffect("SE_VL_Berserk"))
                {
                    ___m_damageMultiplier *= 1.2f;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Attack), "FireProjectileBurst", null)]
        public class ProjectileAttack_Prefix
        {
            public static bool Prefix(Attack __instance, Humanoid ___m_character, ref float ___m_projectileVel, ref float ___m_forceMultiplier, ref float ___m_staggerMultiplier, ref float ___m_damageMultiplier, ref ItemDrop.ItemData ___m_weapon)
            {
                if (___m_character.GetSEMan().HaveStatusEffect("SE_VL_PowerShot"))
                {
                    ___m_projectileVel *= 2f;
                    ___m_damageMultiplier = 1.4f + (___m_character.GetSkills().GetSkillList().FirstOrDefault((Skills.Skill x) => x.m_info == DisciplineSkillDef).m_level * .015f);
                    SE_PowerShot se_shot = ___m_character.GetSEMan().GetStatusEffect("SE_VL_PowerShot") as SE_PowerShot;

                    se_shot.hitCount--;
                    if (se_shot.hitCount <= 0)
                    {
                        ___m_character.GetSEMan().RemoveStatusEffect(se_shot, true);
                    }
                }
                return true;
            }
        }

        //[HarmonyPatch(typeof(Attack), "FireProjectileBurst", null)]
        //public class ProjectileAttack_Postfix
        //{
        //    public static void Postfix(Attack __instance, Humanoid ___m_character, ref float ___m_forceMultiplier, ref float ___m_staggerMultiplier, ref float ___m_damageMultiplier, ref ItemDrop.ItemData ___m_weapon)
        //    {
        //        MethodBase FireProjectileBurst = AccessTools.Method(typeof(Attack), "FireProjectileBurst", null, null);

        //        if (___m_character.GetSEMan().HaveStatusEffect("SE_VL_PowerShot"))
        //        {
        //            //ZLog.Log("last projectile name " + ___m_weapon.m_lastProjectile.name);
        //            SE_PowerShot se_shot = ___m_character.GetSEMan().GetStatusEffect("SE_VL_PowerShot") as SE_PowerShot;

        //            if (se_shot.shouldActivate)
        //            {
        //                se_shot.shouldActivate = false;
        //                se_shot.hitCount--;
        //                if (se_shot.hitCount <= 0)
        //                {
        //                    ___m_character.GetSEMan().RemoveStatusEffect(se_shot, true);
        //                }
        //                //if (___m_weapon.m_lastProjectile != null && ___m_weapon.m_lastProjectile.name.Contains("bow"))
        //                //{
        //                //    //ZLog.Log("fire second shot");

        //                //    ___m_damageMultiplier = .5f + (___m_character.GetSkills().GetSkillList().FirstOrDefault((Skills.Skill x) => x.m_info == DisciplineSkillDef).m_level * .025f);
        //                //    FireProjectileBurst.Invoke(__instance, new object[] { });
        //                //}
        //                //else
        //                //{
                        
        //                ___m_damageMultiplier = 1f + (___m_character.GetSkills().GetSkillList().FirstOrDefault((Skills.Skill x) => x.m_info == DisciplineSkillDef).m_level * .015f);
        //                //}
        //            }
        //            else
        //            {
        //                se_shot.shouldActivate = true;
        //            }
        //        }
        //    }
        //}

        [HarmonyPatch(typeof(ItemDrop.ItemData), "GetBaseBlockPower", new Type[]
        {
            typeof(int)
        })]
        public class BaseBlockPower_Bulwark_Patch
        {
            public static void Postfix(ItemDrop.ItemData __instance, ref float __result)
            {                
                if (Class_Valkyrie.isBlocking)
                {
                    //ZLog.Log("block power " + __result);
                    __result += 20f;
                    //ZLog.Log("modified " + __result);
                }
            }
        }

        [HarmonyPatch(typeof(Skills), "GetSkillDef")]
        public static class GetSkillDef_Patch
        {
            public static void Postfix(Skills __instance, Skills.SkillType type, List<Skills.SkillDef> ___m_skills, ref Skills.SkillDef __result)
            {
                if (__result == null)
                {
                    if(legendsSkills != null)
                    {
                        foreach(Skills.SkillDef sd in legendsSkills)
                        {
                            if(!___m_skills.Contains(sd))
                            {
                                ___m_skills.Add(sd);                                
                            }
                        }
                        __result = ___m_skills.FirstOrDefault((Skills.SkillDef x) => x.m_skill == type);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Localization), "SetupLanguage")]
        public static class EnglishLocalization_Patch
        {
            public static void Postfix(Localization __instance, string language)
            {
                if (language == "English")
                {
                    MethodInfo methodInfo = AccessTools.Method(typeof(Localization), "AddWord", (Type[])null, (Type[])null);
                    methodInfo.Invoke(__instance, new object[2]
                    {
                        "skill_781",
                        "Discipline"
                    });
                    methodInfo.Invoke(__instance, new object[2]
                    {
                        "skill_791",
                        "Abjuration"
                    });
                    methodInfo.Invoke(__instance, new object[2]
                    {
                        "skill_792",
                        "Alteration"
                    });
                    methodInfo.Invoke(__instance, new object[2]
                    {
                        "skill_793",
                        "Conjuration"
                    });
                    methodInfo.Invoke(__instance, new object[2]
                    {
                        "skill_794",
                        "Evocation"
                    });
                }
            }
        }

        [HarmonyPatch(typeof(ZNet), "OnDestroy")]
        public static class RemoveHud_Patch
        {
            public static bool Prefix()
            {
                foreach (RectTransform ability in ValheimLegends.abilitiesStatus)
                {
                    UnityEngine.Object.Destroy(ability.gameObject);
                }
                abilitiesStatus.Clear();
                abilitiesStatus = null;
                return true;
            }
        }


        [HarmonyPatch(typeof(Hud), "UpdateStatusEffects")]
        public static class SkillIcon_Patch
        {
            public static void Postfix(Hud __instance)
            {
                if(__instance != null && ClassIsValid && showAbilityIcons.Value)
                {
                    if(abilitiesStatus == null)
                    {
                        abilitiesStatus = new List<RectTransform>();
                        abilitiesStatus.Clear();                        
                    }
                    if(abilitiesStatus.Count != 3)
                    {
                        foreach (RectTransform ability in ValheimLegends.abilitiesStatus)
                        {
                            UnityEngine.Object.Destroy(ability.gameObject);
                        }
                        ValheimLegends.abilitiesStatus.Clear();
                        VL_Utility.InitiateAbilityStatus(__instance); 
                    }
                    if (abilitiesStatus != null)
                    {
                        for (int j = 0; j < abilitiesStatus.Count; j++)
                        {
                            RectTransform rectTransform2 = abilitiesStatus[j];
                            Image component = rectTransform2.Find("Icon").GetComponent<Image>();
                            string iconText = "";
                            if (j == 0)
                            {
                                component.sprite = Ability1_Sprite;
                                if (Player.m_localPlayer.GetSEMan().HaveStatusEffect("SE_VL_Ability1_CD"))
                                {
                                    component.color = abilityCooldownColor;
                                    iconText = StatusEffect.GetTimeString(Player.m_localPlayer.GetSEMan().GetStatusEffect("SE_VL_Ability1_CD").GetRemaningTime());
                                }
                                else
                                {
                                    component.color = Color.white;
                                    iconText = Ability1_Hotkey.Value;
                                }
                            }
                            else if (j == 1)
                            {

                                component.sprite = Ability2_Sprite;
                                if (Player.m_localPlayer.GetSEMan().HaveStatusEffect("SE_VL_Ability2_CD"))
                                {
                                    component.color = abilityCooldownColor;
                                    iconText = StatusEffect.GetTimeString(Player.m_localPlayer.GetSEMan().GetStatusEffect("SE_VL_Ability2_CD").GetRemaningTime());
                                }
                                else
                                {
                                    component.color = Color.white;
                                    iconText = Ability2_Hotkey.Value;
                                }
                            }
                            else
                            {

                                component.sprite = Ability3_Sprite;
                                if (Player.m_localPlayer.GetSEMan().HaveStatusEffect("SE_VL_Ability3_CD"))
                                {
                                    component.color = abilityCooldownColor;
                                    iconText = StatusEffect.GetTimeString(Player.m_localPlayer.GetSEMan().GetStatusEffect("SE_VL_Ability3_CD").GetRemaningTime());
                                }
                                else
                                {
                                    component.color = Color.white;
                                    iconText = Ability3_Hotkey.Value;
                                }
                            }


                            //rectTransform2.GetComponentInChildren<Text>().text = Localization.instance.Localize((Ability1.Name).ToString());
                            Text component2 = rectTransform2.Find("TimeText").GetComponent<Text>();
                            if (!string.IsNullOrEmpty(iconText))
                            {
                                component2.gameObject.SetActive(value: true);
                                component2.text = iconText;
                            }
                            else
                            {
                                component2.gameObject.SetActive(value: false);
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Skills), "IsSkillValid")]
        public static class ValidSkill_Patch
        {
            public static bool Prefix(Skills __instance, Skills.SkillType type, ref bool __result)
            {
                if (type == AlterationSkill || type == AbjurationSkill || type == ConjurationSkill || type == EvocationSkill || type == DisciplineSkill)
                {
                    __result = true;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Player), "Update")]
        public static class AbilityInput_Prefix
        {
            public static bool Prefix(Player __instance)
            {
                if (ZInput.GetButtonDown("GPower") || ZInput.GetButtonDown("JoyGPower"))
                {
                    ValheimLegends.shouldUseGuardianPower = true;
                }
                return true;
            }
        }
        
        [HarmonyPatch(typeof(Player), "Update", null)]
        public class AbilityInput_Postfix
        {
            public static void Postfix(Player __instance, ref float ___m_maxAirAltitude)
            {
                Player localPlayer = Player.m_localPlayer;
                if (localPlayer != null && VL_Utility.TakeInput(localPlayer) && !localPlayer.InPlaceMode())
                {
                    if (chosenClass.Value.ToString() == "Mage")
                    {                        
                        Class_Mage.Process_Input(localPlayer, ___m_maxAirAltitude);                        
                    }
                    else if(chosenClass.Value.ToString() == "Druid")
                    {
                        Class_Druid.Process_Input(localPlayer, ___m_maxAirAltitude);
                    }
                    else if (chosenClass.Value.ToString() == "Shaman")
                    {                        
                        Class_Shaman.Process_Input(localPlayer);
                    }
                    else if (chosenClass.Value.ToString() == "Ranger")
                    {                        
                        Class_Ranger.Process_Input(localPlayer);
                    }
                    else if (chosenClass.Value.ToString() == "Berserker")
                    {                        
                        Class_Berserker.Process_Input(localPlayer, ref ___m_maxAirAltitude);
                    }
                    else if (chosenClass.Value.ToString() == "Valkyrie")
                    {                        
                        Class_Valkyrie.Process_Input(localPlayer);
                    }
                }

                if (isChargingDash)
                {
                    dashCounter++;
                    if(dashCounter >= 10)
                    {
                        isChargingDash = false;
                        Class_Berserker.Execute_Dash(localPlayer, ref ___m_maxAirAltitude);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Player), "OnSpawned")]
        public static class PlayerMuninNotification_Patch
        {
            public static void Postfix()
            {
                Tutorial.TutorialText vl = new Tutorial.TutorialText
                {
                    m_label = "Valheim Legends",
                    m_name = "Valheim_Legends",
                    m_text = "The Allfather wishes you to know that he has granted you a spark of power as a reward for your past deeds - you need only sacrifice a minor token at a guardian's altar to activate your power.\nFjolner is watching over you.",
                    m_topic = "Become a Legend!"
                };
                if (!Tutorial.instance.m_texts.Contains(vl))
                {
                    Tutorial.instance.m_texts.Add(vl);
                }
                Player.m_localPlayer.ShowTutorial("Valheim_Legends");
            }
        }

        [HarmonyPatch(typeof(PlayerProfile), "LoadPlayerData")]
        public static class LoadSkillsPatch
        {
            public static void Postfix(PlayerProfile __instance, Player player)
            {
                VL_SkillData skillData = __instance.LoadModData<VL_SkillData>();
                if (player.GetSkills().GetSkillList().FirstOrDefault((Skills.Skill x) => x.m_info == DisciplineSkillDef) == null)
                {
                    Skills.Skill skill = (Skills.Skill)AccessTools.Method(typeof(Skills), "GetSkill", (Type[])null, (Type[])null).Invoke(player.GetSkills(), new object[1]
                    {
                        DisciplineSkill
                    });
                    skill.m_level = (float)skillData.level;
                    skill.m_accumulator = skillData.accumulator;                    
                }
                if (player.GetSkills().GetSkillList().FirstOrDefault((Skills.Skill x) => x.m_info == AbjurationSkillDef) == null)
                {
                    Skills.Skill skill = (Skills.Skill)AccessTools.Method(typeof(Skills), "GetSkill", (Type[])null, (Type[])null).Invoke(player.GetSkills(), new object[1]
                    {
                        AbjurationSkill
                    });
                    skill.m_level = (float)skillData.level;
                    skill.m_accumulator = skillData.accumulator;
                }
                if (player.GetSkills().GetSkillList().FirstOrDefault((Skills.Skill x) => x.m_info == AlterationSkillDef) == null)
                {
                    Skills.Skill skill = (Skills.Skill)AccessTools.Method(typeof(Skills), "GetSkill", (Type[])null, (Type[])null).Invoke(player.GetSkills(), new object[1]
                    {
                        AlterationSkill
                    });
                    skill.m_level = (float)skillData.level;
                    skill.m_accumulator = skillData.accumulator;
                }
                if (player.GetSkills().GetSkillList().FirstOrDefault((Skills.Skill x) => x.m_info == ConjurationSkillDef) == null)
                {
                    Skills.Skill skill = (Skills.Skill)AccessTools.Method(typeof(Skills), "GetSkill", (Type[])null, (Type[])null).Invoke(player.GetSkills(), new object[1]
                    {
                        ConjurationSkill
                    });
                    skill.m_level = (float)skillData.level;
                    skill.m_accumulator = skillData.accumulator;
                }
                if (player.GetSkills().GetSkillList().FirstOrDefault((Skills.Skill x) => x.m_info == EvocationSkillDef) == null)
                {
                    Skills.Skill skill = (Skills.Skill)AccessTools.Method(typeof(Skills), "GetSkill", (Type[])null, (Type[])null).Invoke(player.GetSkills(), new object[1]
                    {
                        EvocationSkill
                    });
                    skill.m_level = (float)skillData.level;
                    skill.m_accumulator = skillData.accumulator;
                }
            }
        }

        private void Awake()
        {
            //configs

            modEnabled = this.Config.Bind<bool>("General", "Mod_Enabled", true, "Enable/Disable mod");
            chosenClass = this.Config.Bind<string>("General", "chosenClass", "None");
            showAbilityIcons = this.Config.Bind<bool>("General", "showAbilityIcons", true, "Displays Icons on Hud for each ability");
            iconAlignment = this.Config.Bind<string>("General", "iconAlignment", "horizontal", "Aligns icons horizontally or vertically off the guardian power icon; options are horizontal or vertical");
            Ability1_Hotkey = this.Config.Bind<string>("Keybinds", "Ability1_Hotkey", "Z", "Ability 1 Hotkey\nUse mouse # to bind an ability to a mouse button\nThe # represents the mouse button; mouse 0 is left click, mouse 1 right click, etc");
            Ability1_Hotkey_Combo = this.Config.Bind<string>("Keybinds", "Ability1_Hotkey_Combo", "", "Ability 1 Combination Key - entering a value will trigger the ability only when both the Hotkey and Hotkey_Combo buttons are pressed\nAllows input from a combination of keys when a value is entered for the combo key\nIf only one key is used, leave the combo key blank\nExamples: space, Q, left shift, left ctrl, right alt, right cmd");
            Ability2_Hotkey = this.Config.Bind<string>("Keybinds", "Ability2_Hotkey", "X", "Ability 2 Hotkey");
            Ability2_Hotkey_Combo = this.Config.Bind<string>("Keybinds", "Ability2_Hotkey_Combo", "", "Ability 2 Combination Key");
            Ability3_Hotkey = this.Config.Bind<string>("Keybinds", "Ability3_Hotkey", "C", "Ability 3 Hotkey");
            Ability3_Hotkey_Combo = this.Config.Bind<string>("Keybinds", "Ability3_Hotkey_Combo", "", "Ability 3 Combination Key");
            energyCostMultiplier = this.Config.Bind<float>("Modifiers", "energyCostMultiplier", 1f, "This value multiplied on overall ability use energy cost\nAbility modifiers are not fully implemented");
            cooldownMultiplier = this.Config.Bind<float>("Modifiers", "cooldownMultiplier", 1f, "This value multiplied on overall cooldown time of abilities");
            abilityDamageMultiplier = this.Config.Bind<float>("Modifiers", "abilityDamageMultiplier", 1f, "This value multiplied on overall ability power");
            skillGainMultiplier = this.Config.Bind<float>("Modifiers", "skillGainMultiplier", 1f, "This value modifies the amount of skill experience gained after using an ability");

            

            //assets
            VL_Utility.ModID = "valheim.torann.valheimlegends";
            VL_Utility.Folder = Path.GetDirectoryName(this.Info.Location);
            Texture2D tex_abjuration = VL_Utility.LoadTextureFromAssets("abjuration_skill.png");
            Sprite icon_abjuration = Sprite.Create(tex_abjuration, new Rect(0f, 0f, (float)tex_abjuration.width, (float)tex_abjuration.height), new Vector2(0.5f, 0.5f));
            Texture2D tex_conjuration = VL_Utility.LoadTextureFromAssets("conjuration_skill.png");
            Sprite icon_conjuration = Sprite.Create(tex_conjuration, new Rect(0f, 0f, (float)tex_conjuration.width, (float)tex_conjuration.height), new Vector2(0.5f, 0.5f));
            Texture2D tex_alteration = VL_Utility.LoadTextureFromAssets("alteration_skill.png");
            Sprite icon_alteration = Sprite.Create(tex_alteration, new Rect(0f, 0f, (float)tex_alteration.width, (float)tex_alteration.height), new Vector2(0.5f, 0.5f));
            Texture2D tex_discipline = VL_Utility.LoadTextureFromAssets("discipline_skill.png");
            Sprite icon_discipline = Sprite.Create(tex_discipline, new Rect(0f, 0f, (float)tex_discipline.width, (float)tex_discipline.height), new Vector2(0.5f, 0.5f));
            Texture2D tex_evocation = VL_Utility.LoadTextureFromAssets("evocation_skill.png");
            Sprite icon_evocation = Sprite.Create(tex_evocation, new Rect(0f, 0f, (float)tex_evocation.width, (float)tex_evocation.height), new Vector2(0.5f, 0.5f));

            Texture2D tex_movement = VL_Utility.LoadTextureFromAssets("movement_icon.png");
            Ability3_Sprite = Sprite.Create(tex_movement, new Rect(0f, 0f, (float)tex_movement.width, (float)tex_movement.height), new Vector2(0.5f, 0.5f));
            Texture2D tex_strength = VL_Utility.LoadTextureFromAssets("strength_icon.png");
            Ability2_Sprite = Sprite.Create(tex_strength, new Rect(0f, 0f, (float)tex_strength.width, (float)tex_strength.height), new Vector2(0.5f, 0.5f));
            Texture2D tex_protection = VL_Utility.LoadTextureFromAssets("protection_icon.png");
            Ability1_Sprite = Sprite.Create(tex_protection, new Rect(0f, 0f, (float)tex_protection.width, (float)tex_protection.height), new Vector2(0.5f, 0.5f));

            NameCooldowns();

            //skills            
            AbjurationSkillDef = new Skills.SkillDef
            {
                m_skill = (Skills.SkillType)AbjurationSkillID,
                m_icon = icon_abjuration,
                m_description = "Skill in creating protective spells and wards",
                m_increseStep = 1f
            };
            AlterationSkillDef = new Skills.SkillDef
            {
                m_skill = (Skills.SkillType)AlterationSkillID,
                m_icon = icon_alteration,
                m_description = "Skill in temporarily enhancing or modifying attributes",
                m_increseStep = 1f
            };
            ConjurationSkillDef = new Skills.SkillDef
            {
                m_skill = (Skills.SkillType)ConjurationSkillID,
                m_icon = icon_conjuration,
                m_description = "Skill in temporarily manifesting reality by molding objects and energy",
                m_increseStep = 1f
            };
            DisciplineSkillDef = new Skills.SkillDef
            {
                m_skill = (Skills.SkillType)DisciplineSkillID,
                m_icon = icon_discipline,
                m_description = "Ability to perform or resist phenomenal feats through strength of body and mind",
                m_increseStep = 1f
            };
            EvocationSkillDef = new Skills.SkillDef
            {
                m_skill = (Skills.SkillType)EvocationSkillID,
                m_icon = icon_evocation,
                m_description = "Skill in creating and manipulating energy",
                m_increseStep = 1f
            };
            legendsSkills.Add(DisciplineSkillDef);
            legendsSkills.Add(AbjurationSkillDef);
            legendsSkills.Add(AlterationSkillDef);
            legendsSkills.Add(ConjurationSkillDef);
            legendsSkills.Add(EvocationSkillDef);


            if (modEnabled.Value)
            {
                Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), (string)"valheim.torann.valheimlegends");
            }
        }

        public static void NameCooldowns()
        {
    
            if (chosenClass.Value.ToString() == "Mage")
            {
                ZLog.Log("Valheim Legend: Mage");
                Ability1_Name = "Fireball";
                Ability2_Name = "Frost Nova";
                Ability3_Name = "Meteor";
            }
            else if (chosenClass.Value.ToString() == "Druid")
            {
                ZLog.Log("Valheim Legend: Druid");
                Ability1_Name = "Regeneration";
                Ability2_Name = "Root Defender";
                Ability3_Name = "Wild Vines";
            }
            else if (chosenClass.Value.ToString() == "Shaman")
            {
                ZLog.Log("Valheim Legend: Shaman");
                Ability1_Name = "Enrage";
                Ability2_Name = "Shell";
                Ability3_Name = "Spirit Shock";
            }
            else if (chosenClass.Value.ToString() == "Ranger")
            {
                ZLog.Log("Valheim Legend: Ranger");
                Ability1_Name = "Shadow Stalk";
                Ability2_Name = "Wolf";
                Ability3_Name = "Power Shot";
            }
            else if (chosenClass.Value.ToString() == "Berserker")
            {
                ZLog.Log("Valheim Legend: Berserker");
                Ability1_Name = "Execute";
                Ability2_Name = "Berserk";
                Ability3_Name = "Dash";
            }
            else if (chosenClass.Value.ToString() == "Valkyrie")
            {
                ZLog.Log("Valheim Legend: Valkyrie");
                Ability1_Name = "Bulwark";
                Ability2_Name = "Stagger";
                Ability3_Name = "Leap";
            }
            else
            {
                ZLog.Log("Valheim Legend: --None--");
            }

        }

        public ValheimLegends()
        {
            
        }
    }
}
