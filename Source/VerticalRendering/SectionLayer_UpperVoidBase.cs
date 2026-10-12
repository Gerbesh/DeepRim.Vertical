using Verse;
using DeepRim.Vertical.VerticalWorld;

namespace DeepRim.Vertical.VerticalRendering;

// Native sections own and dispose these meshes. One draw per section replaces per-cell calls.
public sealed class SectionLayer_UpperVoidBase : SectionLayer
{
    public SectionLayer_UpperVoidBase(Section section) : base(section) { relevantChangeTypes = 3UL; }
    public override bool Visible => VerticalRenderContextService.GetLevel(Map) > 0;
    public override void Regenerate()
    {
        ClearSubMeshes(MeshParts.All);
        if (!Visible) return;
        var mask = UpperOverlayVisibilityMask.GetFor(Map);
        var mesh = GetSubMesh(VerticalSectionLayerUtility.UpperVoidNeutralMaterial);
        foreach (var cell in section.CellRect.Cells)
            if (cell.InBounds(Map) && mask.GetCellState(cell, VerticalRuntime.Settings.enableUpperFloorOverlay).ShowNeutralBase)
                VerticalSectionLayerUtility.FillTerrainMainLayerSubMesh(mesh, cell);
        for (var i = 0; i < mesh.verts.Count; i++)
        {
            var vertex = mesh.verts[i]; vertex.y -= .01f; mesh.verts[i] = vertex;
        }
        FinalizeMesh(MeshParts.All);
    }
}
