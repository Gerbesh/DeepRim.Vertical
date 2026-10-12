using System;
using System.Collections.Generic;
using DeepRim.Vertical.VerticalWorld;
using Verse;

namespace DeepRim.Vertical.VerticalConstruction;

public sealed class Building_VerticalStair : Building
{
    public Building_VerticalStair LinkedStair;
    private bool connectionQueued;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref LinkedStair, "linkedStair");
    }

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);
        if (!respawningAfterLoad && !def.GetModExtension<VerticalStairExtension>().isExit)
        {
            QueueConnection();
        }
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        var sourceMap = Map;
        var cell = Position;
        var linked = LinkedStair;
        LinkedStair = null;
        if (linked != null)
        {
            linked.LinkedStair = null;
        }

        if (VerticalSiteWorldComponent.TryGet(out var component)
            && component.TryGetFloor(sourceMap, out _, out var floor))
        {
            component.UnregisterPortal(sourceMap,
                floor.levelIndex + def.GetModExtension<VerticalStairExtension>().levelDelta, cell);
        }

        base.DeSpawn(mode);
        if (linked?.Spawned == true)
        {
            // Exits have no resource cost; refund the paid end once when deconstructing either end.
            linked.Destroy(def.GetModExtension<VerticalStairExtension>().isExit
                && mode == DestroyMode.Deconstruct ? DestroyMode.Deconstruct : DestroyMode.Vanish);
        }
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (var gizmo in base.GetGizmos())
        {
            yield return gizmo;
        }

        if (LinkedStair == null && !def.GetModExtension<VerticalStairExtension>().isExit)
        {
            yield return new Command_Action
            {
                defaultLabel = "DeepRimVertical.Stairs.RetryConnection".Translate(),
                defaultDesc = "DeepRimVertical.Stairs.RetryConnectionDescription".Translate(),
                action = QueueConnection
            };
        }
    }

    private void QueueConnection()
    {
        if (connectionQueued || !Spawned || LinkedStair != null)
        {
            return;
        }

        connectionQueued = true;
        LongEventHandler.QueueLongEvent(() =>
        {
            try
            {
                if (Spawned && LinkedStair == null)
                {
                    VerticalStairService.Connect(this);
                }
            }
            finally
            {
                connectionQueued = false;
            }
        }, "GeneratingMap", false, exception =>
        {
            connectionQueued = false;
            Log.Error($"[DeepRim Vertical] Could not connect staircase: {exception}");
        });
    }
}
