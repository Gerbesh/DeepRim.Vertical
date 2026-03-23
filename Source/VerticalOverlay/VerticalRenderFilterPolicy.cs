using RimWorld;
using Verse;

namespace DeepRim.Vertical.VerticalOverlay;

public static class VerticalRenderFilterPolicy
{
    public static bool ShouldRenderVisualThing(Thing thing)
    {
        var def = ResolveThingDef(thing);
        if (def == null || thing == null || !thing.Spawned || thing.Destroyed)
        {
            return false;
        }

        if (thing is Pawn or Blueprint or Frame)
        {
            return false;
        }

        return def.category is not (ThingCategory.Mote or ThingCategory.Filth or ThingCategory.Projectile or ThingCategory.Gas or ThingCategory.Attachment or ThingCategory.Ethereal or ThingCategory.PsychicEmitter);
    }

    public static bool ShouldRenderSupportThing(Thing thing)
    {
        var def = ResolveThingDef(thing);
        if (def == null)
        {
            return false;
        }

        if (HasKeyword(def, "stair", "shaft", "column", "pillar", "support", "anchor"))
        {
            return true;
        }

        return def.IsEdifice() && (def.passability == Traversability.Impassable || HasKeyword(def, "wall"));
    }

    public static bool ShouldRenderStructureThing(Thing thing)
    {
        var def = ResolveThingDef(thing);
        if (def == null)
        {
            return false;
        }

        if (ShouldRenderSupportThing(thing))
        {
            return true;
        }

        return def.category == ThingCategory.Building
               && (def.IsDoor || def.IsBed || def.building != null && (def.Size.x * def.Size.z >= 2 || def.passability == Traversability.Impassable || !def.building.isNaturalRock));
    }

    public static bool ShouldRenderPawn(Pawn pawn)
    {
        return pawn != null && pawn.Spawned && !pawn.Dead;
    }

    public static ThingDef ResolveThingDef(Thing thing)
    {
        if (thing == null)
        {
            return null;
        }

        if (thing.def.entityDefToBuild is ThingDef buildThingDef)
        {
            return buildThingDef;
        }

        return thing.def;
    }

    private static bool HasKeyword(ThingDef def, params string[] keywords)
    {
        var source = ((def.defName ?? string.Empty) + " " + (def.label ?? string.Empty)).ToLowerInvariant();
        foreach (var keyword in keywords)
        {
            if (source.Contains(keyword))
            {
                return true;
            }
        }

        return false;
    }
}
