using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalColony;

// Only a product-count query sees a different Bill.Map; real bill/job owners never move.
internal static class BillCountScope
{
    [ThreadStatic] public static Bill Bill;
    [ThreadStatic] public static Map Map;
}
[HarmonyPatch(typeof(Bill), "get_Map")]
public static class VerticalBillCountMapPatch
{
    public static void Postfix(Bill __instance, ref Map __result)
    { if (BillCountScope.Bill == __instance && BillCountScope.Map != null) __result = BillCountScope.Map; }
}
[HarmonyPatch]
public static class VerticalProductionCountPatch
{
    public static IEnumerable<MethodBase> TargetMethods() => typeof(RecipeWorkerCounter).Assembly.GetTypes()
        .Where(t => typeof(RecipeWorkerCounter).IsAssignableFrom(t))
        .Select(t => AccessTools.DeclaredMethod(t, nameof(RecipeWorkerCounter.CountProducts), new[] { typeof(Bill_Production) }))
        .Where(m => m != null && !m.IsAbstract).Distinct();
    public static bool Prefix(RecipeWorkerCounter __instance, Bill_Production bill, ref int __result)
    {
        if (BillCountScope.Bill != null || bill.GetIncludeSlotGroup() != null || !VerticalColonyUtility.TrySite(bill.Map, out var site)) return true;
        var previousBill = BillCountScope.Bill; var previousMap = BillCountScope.Map;
        var previousResources = ColonyQueryScope.Resources;
        try
        {
            ColonyQueryScope.Resources = 0;
            BillCountScope.Bill = bill;
            var count = 0;
            foreach (var map in VerticalColonyUtility.Maps(site))
            { BillCountScope.Map = map; count += __instance.CountProducts(bill); }
            __result = count; return false;
        }
        finally { BillCountScope.Bill = previousBill; BillCountScope.Map = previousMap; ColonyQueryScope.Resources = previousResources; }
    }
}
