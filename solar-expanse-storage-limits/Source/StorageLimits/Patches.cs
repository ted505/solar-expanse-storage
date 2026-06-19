using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Data.ScriptableObject;
using Extensions;
using Game.ObjectInfoDataScripts;
using Game.UI.Windows.Elements;
using Game.UI.Windows.Elements.ObjectInfoElements;
using Game.UI.Windows.Windows;
using HarmonyLib;
using Language;
using Manager;
using UnityEngine;
using UnityEngine.UI;

namespace StorageLimits
{
    [HarmonyPatch(typeof(ObjectInfoManager), "Update")]
    static class PatchObjectInfoManagerUpdate
    {
        static bool _ran = false;

        static readonly FieldInfo _eventWasField = typeof(ObjectInfoManager)
            .GetField("solarSystemLoadEventWas",
                      BindingFlags.NonPublic | BindingFlags.Instance);

        static void Postfix(ObjectInfoManager __instance)
        {
            if (_ran) return;
            if (_eventWasField == null)
            {
                Plugin.Log.LogError("[StorageLimits] solarSystemLoadEventWas field not found.");
                _ran = true;
                return;
            }

            bool eventFired = (bool)_eventWasField.GetValue(__instance);
            if (!eventFired) return;

            _ran = true;

            var configPath = Path.Combine(Plugin.PluginDir, "storage_limits.yaml");
            StorageTracker.Config = StorageConfig.Load(configPath);

            Plugin.Log.LogInfo("[StorageLimits] Initialized — production throttling active.");
        }
    }

    [HarmonyPatch(typeof(Facility), nameof(Facility.FinalEfficiencyBasedOnPowerDeliveryAndWorkforceAllocationAndResources), MethodType.Getter)]
    static class PatchFacilityEfficiency
    {
        static void Postfix(Facility __instance, ref double __result)
        {
            if (StorageTracker.Config == null)
                return;

            double storageEff = StorageTracker.GetStorageEfficiency(__instance);
            if (storageEff < 1.0)
                __result *= storageEff;
        }
    }

    [HarmonyPatch(typeof(Facility), nameof(Facility.GetResourceEfficiency))]
    static class PatchFacilityResourceEfficiency
    {
        static void Postfix(Facility __instance, ref double __result)
        {
            if (StorageTracker.Config == null)
                return;

            var descriptor = __instance.facilityDescriptor;
            if (descriptor == null)
                return;

            if (!StorageTracker.Config.Facilities.ContainsKey(descriptor.ID))
                return;

            if (descriptor.EnergyConsumption == 0 && descriptor.NeedWorkersToWork(__instance.Company) == 0)
                return;

            __result = StorageTracker.GetFillRatio(__instance);
        }
    }

    [HarmonyPatch(typeof(ObjectInfoData), "OnEachDayUpdate")]
    static class PatchObjectInfoDataSpoilage
    {
        static void Postfix(ObjectInfoData __instance, double dt)
        {
            if (StorageTracker.Config == null)
                return;

            if (dt <= 0)
                return;

            try
            {
                StorageTracker.ProcessSpoilage(__instance, dt);
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug($"[StorageLimits] Spoilage error: {e.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(UIRowResources), "SetData")]
    static class PatchResourceRowSetData
    {
        static void Postfix(UIRowResources __instance)
        {
            StorageFillBar.UpdateBar(__instance);
        }
    }

    [HarmonyPatch(typeof(UIRowResources), "RowResourcesDataOnOnValueChange")]
    static class PatchResourceRowValueChange
    {
        static void Postfix(UIRowResources __instance)
        {
            StorageFillBar.UpdateBar(__instance);
        }
    }

    [HarmonyPatch(typeof(UIRowResources), "GetTooltipStringStatic")]
    static class PatchResourceRowTooltip
    {
        static void Postfix(RowResourcesData rowResourcesData, UIWindow parentWindow, ref string __result)
        {
            if (StorageTracker.Config == null)
                return;

            ObjectInfoData oid = rowResourcesData?.ObjectInfoData;
            if (oid == null && parentWindow is ObjectInfoWindow oiw)
                oid = oiw.ObjectInfoDataCurrent;

            if (oid == null)
                return;

            __result = StorageTracker.AppendStorageToTooltip(rowResourcesData, oid, __result);
        }
    }

    [HarmonyPatch(typeof(FacilityBaseDescriptor), "GetFacilityStats")]
    static class PatchGetFacilityStatsStorage
    {
        private const int MaxItemized = 4;

        static void Postfix(FacilityBaseDescriptor __instance, ref List<(string, string)> __result)
        {
            if (StorageTracker.Config == null || __instance == null)
                return;

            if (!StorageTracker.Config.Facilities.TryGetValue(__instance.ID, out var grants))
                return;

            if (grants.Count == 0)
                return;

            string fmt = LEManager.Get("UI.MassFormat");

            var byAmount = grants
                .GroupBy(kvp => kvp.Value)
                .OrderByDescending(g => g.Key)
                .ToList();

            bool allSame = byAmount.Count == 1;

            if (allSame)
            {
                double amount = byAmount[0].Key;
                int count = byAmount[0].Count();
                string value = "+" + amount.ToPostfixString(fmt) + " (" + count + " resources)";
                __result.Add(("Storage", value));
            }
            else
            {
                int shown = 0;
                int remaining = 0;

                foreach (var group in byAmount)
                {
                    foreach (var kvp in group)
                    {
                        if (shown < MaxItemized)
                        {
                            string name = LEManager.Get(kvp.Key);
                            string value = "+" + kvp.Value.ToPostfixString(fmt) + " " + name;
                            __result.Add(("Storage", value));
                            shown++;
                        }
                        else
                        {
                            remaining++;
                        }
                    }
                }

                if (remaining > 0)
                    __result.Add(("Storage", "+" + remaining + " more"));
            }
        }
    }

    static class StorageFillBar
    {
        private const string BarName = "StorageLimitsFillBar";
        private const string BgName = "StorageLimitsFillBg";
        private const float BarHeight = 3f;

        private static readonly Color ColorGreen = new Color(0.608f, 0.878f, 0.482f, 0.9f);
        private static readonly Color ColorYellow = new Color(1f, 0.839f, 0.42f, 0.9f);
        private static readonly Color ColorRed = new Color(1f, 0.541f, 0.4f, 0.9f);
        private static readonly Color ColorBg = new Color(0.15f, 0.15f, 0.15f, 0.6f);

        internal static void UpdateBar(UIRowResources row)
        {
            if (StorageTracker.Config == null || row == null)
                return;

            var rowData = row.ResourcesData;
            if (rowData == null)
                return;

            ObjectInfoData oid = rowData.ObjectInfoData;
            if (oid == null)
            {
                var parentWindow = Traverse.Create((ListElement)row).Field("parentWindow").GetValue<UIWindow>() as ObjectInfoWindow;
                oid = parentWindow?.ObjectInfoDataCurrent;
            }

            double cap = StorageTracker.GetCapForRow(rowData, oid);

            Image iconImage = Traverse.Create(row).Field("imageResource").GetValue<Image>();
            if (iconImage == null)
                return;

            RectTransform iconRect = iconImage.rectTransform;

            if (double.IsPositiveInfinity(cap))
            {
                HideBar(iconRect);
                return;
            }

            double fill = cap > 0 ? rowData.Value / cap : 1.0;
            float fillClamped = Mathf.Clamp01((float)fill);

            Color barColor;
            if (cap == 0 || fill >= 1.0)
                barColor = ColorRed;
            else if (fill >= 0.95)
                barColor = ColorYellow;
            else
                barColor = ColorGreen;

            // Show a full red bar when cap is zero
            if (cap == 0)
                fillClamped = 1f;

            EnsureBar(iconRect, fillClamped, barColor);
        }

        private static void EnsureBar(RectTransform parent, float fill, Color color)
        {
            Transform bgTransform = parent.Find(BgName);
            Image bgImage;
            Image fillImage;

            if (bgTransform == null)
            {
                var bgGo = new GameObject(BgName);
                bgGo.transform.SetParent(parent, false);
                bgImage = bgGo.AddComponent<Image>();
                bgImage.color = ColorBg;
                bgImage.raycastTarget = false;
                var bgRect = bgGo.GetComponent<RectTransform>();
                bgRect.anchorMin = new Vector2(0, 0);
                bgRect.anchorMax = new Vector2(1, 0);
                bgRect.pivot = new Vector2(0, 0);
                bgRect.anchoredPosition = new Vector2(0, -2f);
                bgRect.sizeDelta = new Vector2(0, BarHeight);

                var fillGo = new GameObject(BarName);
                fillGo.transform.SetParent(bgRect, false);
                fillImage = fillGo.AddComponent<Image>();
                fillImage.raycastTarget = false;
                var fillRect = fillGo.GetComponent<RectTransform>();
                fillRect.anchorMin = new Vector2(0, 0);
                fillRect.anchorMax = new Vector2(0, 1);
                fillRect.pivot = new Vector2(0, 0);
                fillRect.anchoredPosition = Vector2.zero;
                fillRect.sizeDelta = Vector2.zero;
            }
            else
            {
                bgImage = bgTransform.GetComponent<Image>();
                fillImage = bgTransform.Find(BarName)?.GetComponent<Image>();
                if (fillImage == null)
                    return;
            }

            bgTransform = bgImage.rectTransform;
            bgTransform.gameObject.SetActive(true);

            var barRect = fillImage.rectTransform;
            barRect.anchorMax = new Vector2(fill, 1);
            fillImage.color = color;
        }

        private static void HideBar(RectTransform parent)
        {
            Transform bg = parent.Find(BgName);
            if (bg != null)
                bg.gameObject.SetActive(false);
        }
    }
}
