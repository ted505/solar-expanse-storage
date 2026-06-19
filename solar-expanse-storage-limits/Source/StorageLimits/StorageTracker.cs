using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using Data.ScriptableObject;
using Extensions;
using Game.Info;
using Game.ObjectInfoDataScripts;
using Game.ObjectInfoDataScripts.CustomFacilitiesAndModules;
using Game.UI.Windows.Elements.ObjectInfoElements;
using Language;
using Manager;
using ScriptableObjectScripts;

namespace StorageLimits
{
    internal static class StorageTracker
    {
        internal static StorageConfig Config;

        internal static double GetCap(ObjectInfoData oid, ResourceDefinition resource)
        {
            if (Config == null || oid == null || resource == null)
                return double.PositiveInfinity;

            if (oid.company != MonoBehaviourSingleton<GameManager>.Instance.Player)
                return double.PositiveInfinity;

            if (!Config.IsManaged(resource.ID))
                return double.PositiveInfinity;

            var objType = oid.ObjectInfo?.objectTypes ?? EObjectTypes.None;
            bool isOrbital = objType == EObjectTypes.Orbit || objType == EObjectTypes.SolarOrbit;

            var placement = Config.GetPlacement(resource.ID);
            if (placement != EStoragePlacement.Both)
            {
                if (placement == EStoragePlacement.Surface && isOrbital)
                    return double.PositiveInfinity;
                if (placement == EStoragePlacement.Orbit && !isOrbital)
                    return double.PositiveInfinity;
            }

            double cap = isOrbital ? Config.BaseCapacityOrbit : Config.BaseCapacity;

            var facilities = oid.ListFacility;
            if (facilities != null)
            {
                foreach (var facility in facilities)
                {
                    if (facility == null || !facility.FinishConstructionBool)
                        continue;

                    var descriptor = facility.facilityDescriptor;
                    if (descriptor == null)
                        continue;

                    if (!Config.Facilities.TryGetValue(descriptor.ID, out var grants))
                        continue;

                    if (grants.TryGetValue(resource.ID, out double amount))
                        cap += amount * facility.Quantity;
                }
            }

            // Docked spacecraft contribute their fuel tank capacity
            // for their propellant resource type.
            var spacecraft = oid.ListSpaceCrafts;
            if (spacecraft != null)
            {
                foreach (var sc in spacecraft)
                {
                    if (sc?.spacecraftType == null)
                        continue;

                    var fuelType = sc.spacecraftType.GetFuelType();
                    if (fuelType != null && fuelType == resource)
                        cap += sc.spacecraftType.GetFuelCapacity(oid.company);
                }
            }

            return cap;
        }

        internal static double GetStorageEfficiency(Facility facility)
        {
            if (Config == null || facility == null)
                return 1.0;

            var oid = facility.ObjectInfoData;
            if (oid == null)
                return 1.0;

            var outputResources = GetOutputResources(facility);
            if (outputResources == null || outputResources.Count == 0)
                return 1.0;

            double worstRatio = 1.0;
            const double rampFraction = 0.05;

            foreach (var resource in outputResources)
            {
                double cap = GetCap(oid, resource);
                if (double.IsPositiveInfinity(cap))
                    continue;

                double current = GetCurrentStock(oid, resource);
                double remaining = cap - current;

                if (remaining <= 0.0)
                    return 0.0;

                double rampZone = cap * rampFraction;
                double ratio = (remaining < rampZone)
                    ? remaining / rampZone
                    : 1.0;

                if (ratio < worstRatio)
                    worstRatio = ratio;
            }

            return worstRatio;
        }

        private static double GetCurrentStock(ObjectInfoData oid, ResourceDefinition resource)
        {
            if (oid.ListRowResourcesData == null)
                return 0.0;

            double total = 0.0;
            foreach (var row in oid.ListRowResourcesData)
            {
                if (row.ResourcesType == resource)
                    total += row.Value;
            }
            return total;
        }

        internal static double GetCapForRow(RowResourcesData row, ObjectInfoData oid)
        {
            if (Config == null || row == null || oid == null)
                return double.PositiveInfinity;

            var resource = row.ResourcesType;
            if (resource == null || resource.ResourceType != ResourceDefinition.EResourceType.Normal)
                return double.PositiveInfinity;

            return GetCap(oid, resource);
        }

        internal static double GetFillRatio(Facility facility)
        {
            if (Config == null || facility == null)
                return 1.0;

            var descriptor = facility.facilityDescriptor;
            if (descriptor == null)
                return 1.0;

            if (!Config.Facilities.TryGetValue(descriptor.ID, out var grants))
                return 1.0;

            var oid = facility.ObjectInfoData;
            if (oid == null)
                return 1.0;

            double totalFill = 0.0;
            int count = 0;

            foreach (var kvp in grants)
            {
                string resourceId = kvp.Key;
                double grantPerUnit = kvp.Value;
                double cap = Config.BaseCapacity + grantPerUnit * facility.Quantity;

                double current = 0.0;
                if (oid.ListRowResourcesData != null)
                {
                    foreach (var row in oid.ListRowResourcesData)
                    {
                        if (row.ResourcesType != null && row.ResourcesType.ID == resourceId)
                        {
                            current += row.Value;
                            break;
                        }
                    }
                }

                if (cap > 0)
                {
                    totalFill += Math.Min(current / cap, 1.0);
                    count++;
                }
            }

            return count > 0 ? totalFill / count : 0.0;
        }

        internal static string FormatStorageText(RowResourcesData row, ObjectInfoData oid)
        {
            double cap = GetCapForRow(row, oid);
            if (double.IsPositiveInfinity(cap))
                return null;

            string fmt = Language.LEManager.Get("UI.MassFormat");
            string currentStr = row.Value.ToPostfixString(fmt);
            string capStr = cap.ToPostfixString(fmt);

            string color;
            if (row.Value >= cap)
                color = "#FF8A66";
            else if (row.Value >= cap * 0.95)
                color = "#FFD66B";
            else
                color = "#9BE07B";

            return currentStr + "/<color=" + color + ">" + capStr + "</color>";
        }

        internal static string AppendStorageToTooltip(RowResourcesData row, ObjectInfoData oid, string tooltip)
        {
            double cap = GetCapForRow(row, oid);
            if (double.IsPositiveInfinity(cap))
                return tooltip;

            string fmt = Language.LEManager.Get("UI.MassFormat");
            double remaining = Math.Max(0.0, cap - row.Value);
            double fillPct = cap > 0 ? (row.Value / cap) * 100.0 : 0.0;

            string color;
            if (row.Value >= cap)
                color = "#FF8A66";
            else if (row.Value >= cap * 0.95)
                color = "#FFD66B";
            else
                color = "#9BE07B";

            return tooltip
                + Environment.NewLine
                + Environment.NewLine
                + "<color=" + color + ">Storage: "
                + row.Value.ToPostfixString(fmt)
                + " / "
                + cap.ToPostfixString(fmt)
                + " (" + fillPct.ToString("F0") + "%)</color>"
                + Environment.NewLine
                + "Remaining: " + remaining.ToPostfixString(fmt);
        }

        private static HashSet<ResourceDefinition> GetOutputResources(Facility facility)
        {
            var result = new HashSet<ResourceDefinition>();
            var descriptor = facility.facilityDescriptor;
            if (descriptor == null)
                return result;

            if (descriptor.Byproducts != null)
            {
                foreach (var bp in descriptor.Byproducts)
                {
                    if (bp.resource != null)
                        result.Add(bp.resource);
                }
            }

            if (facility is MiningFacility
                || descriptor.specialAbilityFacilityNew.HasFlag(ESpecialAbilityFacilityNew.Mining))
            {
                try
                {
                    var company = facility.Company;
                    var toMine = descriptor.GetResourcesToMine(company);
                    if (toMine != null)
                        result.UnionWith(toMine);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogDebug($"[StorageTracker] Could not get mining resources for {descriptor.ID}: {e.Message}");
                }
            }

            if (descriptor.refinerData != null
                && (facility is RefineryFacility
                    || descriptor.specialAbilityFacilityNew.HasFlag(ESpecialAbilityFacilityNew.Refiner)))
            {
                try
                {
                    var company = facility.Company;
                    var outputs = descriptor.refinerData.GetOutput(company, descriptor);
                    if (outputs != null)
                    {
                        foreach (var item in outputs)
                        {
                            if (item.resource != null)
                                result.Add(item.resource);
                        }
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogDebug($"[StorageTracker] Could not get refiner outputs for {descriptor.ID}: {e.Message}");
                }
            }

            return result;
        }

        internal static void ProcessSpoilage(ObjectInfoData oid, double days)
        {
            if (Config == null || oid == null || days <= 0.0)
                return;

            var rows = oid.ListRowResourcesData;
            if (rows == null)
                return;

            var objectInfo = oid.ObjectInfo;
            if (objectInfo == null)
                return;

            foreach (var row in rows)
            {
                if (row.ResourcesType == null
                    || row.ResourcesType.ResourceType != ResourceDefinition.EResourceType.Normal)
                    continue;

                double cap = GetCap(oid, row.ResourcesType);
                if (double.IsPositiveInfinity(cap))
                    continue;

                double excess = row.Value - cap;
                if (excess <= 0.0)
                    continue;

                double rate = Config.GetSpoilageRate(row.ResourcesType.ID);
                if (rate <= 0.0)
                    continue;

                // Amount to transfer this tick (fraction of excess per day × elapsed days)
                double toTransfer = Math.Min(excess * rate * days, excess);
                if (toTransfer <= 0.0)
                    continue;

                row.Value -= toTransfer;

                if (row.Value < 0.5)
                {
                    toTransfer += row.Value;
                    row.Value = 0.0;
                }

                // Only return to deposits if the game considers this a
                // deposit-eligible resource; manufactured goods are simply lost.
                if (row.ResourcesType.CanBeLeftOnObject)
                    TransferToDeposit(oid, objectInfo, row.ResourcesType, toTransfer);
            }
        }

        private static void TransferToDeposit(
            ObjectInfoData oid, ObjectInfo objectInfo,
            ResourceDefinition resource, double amount)
        {
            // Find an existing deposit with the standard starting mining factor
            float startingMiningFactor = MonoBehaviourSingleton<GameManager>.Instance
                .Economic.ResourceDepositStartingMiningFactor;

            RowResourcesData deposit = null;
            foreach (var explored in oid.listExploredResourcesRows)
            {
                if (explored.ObservedData.ResourcesType == resource
                    && explored.ExploredInAnyCapacity
                    && explored.ObservedData.MiningFactor.HasValue
                    && Math.Abs(explored.ObservedData.MiningFactor.Value - startingMiningFactor) < 0.001f)
                {
                    deposit = explored.ObservedData;
                    break;
                }
            }

            if (deposit != null)
            {
                deposit.Value += amount;
            }
            else
            {
                // Create a new deposit on the ObjectInfo's shared deposit list
                deposit = new RowResourcesData
                {
                    ResourcesType = resource,
                    Value = amount,
                    MiningFactor = startingMiningFactor,
                    ResourceState = RowResourcesData.EResourceState.Solid,
                    ObjectInfoData = oid
                };
                objectInfo.ListRowResourcesData.Add(deposit);

                // Create matching explored-resource entries for ALL companies at this location
                foreach (var companyOid in objectInfo.ObjectsInfoData)
                {
                    companyOid.listExploredResourcesRows.Add(new RowExploredResourcesData
                    {
                        ObservedData = deposit,
                        Value = 1.0f,
                        PreliminaryExplored = true,
                        ResourceType = resource
                    });
                }
            }

            try
            {
                objectInfo.InvokeOnAddOrChangeDeposit(deposit, amount,
                    MonoBehaviourSingleton<GameManager>.Instance.Player);
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug($"[StorageTracker] InvokeOnAddOrChangeDeposit failed: {e.Message}");
            }
        }
    }
}
