using System;
using System.Collections.Generic;
using DeepRim.Vertical.Persistence;
using DeepRim.Vertical.VerticalColony;
using DeepRim.Vertical.VerticalWorld;
using Verse;
using UnityEngine;

namespace DeepRim.Vertical.VerticalCombat;

public sealed class VerticalRaySegment
{
    public Map Map, NextMap;
    public Vector3 Start, End;
    public int LowerBoundary;
}
public static class VerticalSight
{
    public const float FloorHeight = 3f;
    public static bool SameSite(Map a, Map b, out VerticalSiteRecord site)
    {
        site = null;
        if (!VerticalColonyUtility.TrySite(a, out var first) || !VerticalColonyUtility.TrySite(b, out var second) || first != second) return false;
        site = first; return true;
    }
    public static bool Open(VerticalSiteRecord site, int lowerLevel, IntVec3 cell)
    {
        var lower = site.FloorAt(lowerLevel)?.Map; var upper = site.FloorAt(lowerLevel + 1)?.Map;
        if (lower == null || upper == null || !cell.InBounds(lower) || !cell.InBounds(upper)) return false;
        // A staircase is a passage, not a line-of-fire hole. Both physical boundaries must be open.
        return upper.terrainGrid.TerrainAt(cell) == VerticalDefOf.DeepRimVertical_UpperVoid && !cell.Roofed(lower)
            && cell.CanBeSeenOverFast(upper) && cell.CanBeSeenOverFast(lower);
    }
    public static List<VerticalRaySegment> Segments(VerticalSiteRecord site, int fromLevel, int toLevel, Vector3 from, Vector3 to, float startFraction = 0f, float endFraction = 1f)
    {
        var result = new List<VerticalRaySegment>();
        var level = (int)Math.Floor(fromLevel + .5f + (toLevel - fromLevel) * (startFraction + .000001f));
        var direction = Math.Sign(toLevel - fromLevel);
        var at = Vector3.Lerp(from, to, startFraction);
        if (direction != 0)
        {
            while (level != toLevel)
            {
                var boundary = level + (direction > 0 ? 1 : 0);
                var fraction = (boundary - (fromLevel + .5f)) / (toLevel - fromLevel);
                if (fraction > endFraction) break;
                var next = Vector3.Lerp(from, to, fraction);
                var map = site.FloorAt(level)?.Map; var nextMap = site.FloorAt(level + direction)?.Map;
                if (map == null || nextMap == null) return null;
                result.Add(new VerticalRaySegment { Map = map, NextMap = nextMap, Start = at, End = next, LowerBoundary = Math.Min(level, level + direction) });
                at = next; level += direction;
            }
        }
        var finalMap = site.FloorAt(level)?.Map;
        if (finalMap == null) return null;
        result.Add(new VerticalRaySegment { Map = finalMap, Start = at, End = Vector3.Lerp(from, to, endFraction) });
        return result;
    }
    public static float Distance(Thing from, Thing target)
    {
        if (!SameSite(from.Map, target.Map, out var site)) return float.PositiveInfinity;
        var difference = (site.floors.Find(f => f.Map == from.Map).levelIndex - site.floors.Find(f => f.Map == target.Map).levelIndex) * FloorHeight;
        return Mathf.Sqrt(from.Position.DistanceToSquared(target.Position) + difference * difference);
    }
    public static bool CanShoot(Verb verb, IntVec3 root, Thing target, bool ignoreRange = false)
    {
        if (verb is not Verb_LaunchProjectile launch || launch.Projectile?.projectile == null || launch.Projectile.projectile.flyOverhead
            || verb.verbProps.IsMeleeAttack || target?.Spawned != true || verb.Caster?.Spawned != true
            || !SameSite(verb.Caster.Map, target.Map, out var site)) return false;
        if (verb.Caster is Pawn shooter && shooter.WorkTagIsDisabled(WorkTags.Violent)) return false;
        var fromLevel = site.floors.Find(f => f.Map == verb.Caster.Map).levelIndex;
        var toLevel = site.floors.Find(f => f.Map == target.Map).levelIndex;
        var distance = Mathf.Sqrt(root.DistanceToSquared(target.Position) + Mathf.Pow((toLevel - fromLevel) * FloorHeight, 2));
        if (!ignoreRange && (distance > verb.EffectiveRange * VerticalHeightAdvantage.RangeMultiplier(fromLevel - toLevel)
            || distance < verb.verbProps.EffectiveMinRange(target, verb.Caster))) return false;
        if (verb.Caster.Faction == RimWorld.Faction.OfPlayer && target.Position.Fogged(target.Map)) return false;
        var segments = Segments(site, fromLevel, toLevel, root.ToVector3Shifted(), target.Position.ToVector3Shifted());
        if (segments == null) return false;
        foreach (var segment in segments)
        {
            var first = segment.Start.ToIntVec3(); var last = segment.End.ToIntVec3();
            if (!first.InBounds(segment.Map) || !last.InBounds(segment.Map)
                || !GenSight.LineOfSight(first, last, segment.Map, true, c => c == target.Position && segment.Map == target.Map
                    || c.CanBeSeenOverFast(segment.Map) && !c.AnyGas(segment.Map, GasType.BlindSmoke))) return false;
            if (segment.NextMap != null && !Open(site, segment.LowerBoundary, last)) return false;
        }
        return true;
    }
}
