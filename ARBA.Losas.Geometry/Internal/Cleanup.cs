using System.Collections.Generic;

namespace ARBA.Losas.Geometry.Internal
{
    /// <summary>
    /// Fusiona vertices consecutivos mas cercanos que la tolerancia de curva corta. La
    /// fusion es global (alias en el registro), asi que las regiones vecinas siguen
    /// compartiendo el mismo punto.
    /// </summary>
    internal static class Cleanup
    {
        public static void CollapseShortEdges(IEnumerable<Region> regions, PointRegistry reg, double shortTolerance, List<GeometryWarning> warnings)
        {
            var list = new List<Region>(regions);
            bool changed = true;
            int guard = 0;
            while (changed && guard++ < 1000)
            {
                changed = false;
                foreach (Region region in list)
                    foreach (Ring ring in region.AllRings())
                    {
                        List<RegistryPoint> pts = Region.ResolvedDistinct(ring, reg);
                        if (pts.Count < 2) continue;
                        for (int i = 0; i < pts.Count; i++)
                        {
                            RegistryPoint a = pts[i], b = pts[(i + 1) % pts.Count];
                            if (ReferenceEquals(a, b)) continue;
                            if (a.Position.DistanceTo(b.Position) >= shortTolerance) continue;
                            // dos vertices originales tan proximos vienen asi del boceto: se respetan
                            if (a.Kind == VertexKind.Original && b.Kind == VertexKind.Original) continue;
                            RegistryPoint survivor = a.Kind <= b.Kind ? a : b;
                            RegistryPoint victim = ReferenceEquals(survivor, a) ? b : a;
                            reg.Merge(victim, survivor);
                            warnings.Add(new GeometryWarning(WarningKind.ShortEdgeCollapsed,
                                "Vertice " + victim.Position + " fusionado con " + survivor.Position +
                                " por quedar a menos de la tolerancia de curva corta."));
                            changed = true;
                            break;
                        }
                        if (changed) break;
                    }
            }
        }
    }
}
