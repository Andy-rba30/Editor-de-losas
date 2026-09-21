using System;
using System.Collections.Generic;
using ARBA.Losas.Geometry.Internal;
using Clipper2Lib;

namespace ARBA.Losas.Geometry
{
    /// <summary>
    /// Une varias regiones (losas) en una con Clipper2 (FillRule.NonZero). Antes de unir,
    /// los vertices de una losa a menos de la tolerancia de union de un vertice o borde de
    /// otra se ajustan para cerrar microgaps conservando las curvas originales; si aun asi
    /// quedan regiones separadas se prueba un inflado y desinflado con esa tolerancia.
    /// </summary>
    public static class SlabMerger
    {
        public static MergeResult Merge(IReadOnlyList<Polygon2> slabs, MergeOptions options)
        {
            if (slabs == null) throw new ArgumentNullException(nameof(slabs));
            if (slabs.Count < 2) throw new ArgumentException("Hacen falta al menos dos losas para unir.");
            options ??= new MergeOptions();
            if (!(options.Scale > 0)) throw new ArgumentException("Scale debe ser positivo.");
            if (!(options.TessellationTolerance > 0)) throw new ArgumentException("TessellationTolerance debe ser positiva.");
            if (!(options.JoinTolerance >= 0)) throw new ArgumentException("JoinTolerance no puede ser negativa.");

            var warnings = new List<GeometryWarning>();
            var table = new CurveTable();
            var reg = new PointRegistry(options.Scale);
            double classifyTol = EdgeClassifier.Tolerance(options.TessellationTolerance, options.Scale);
            var mapper = new OutputMapper(reg, table, classifyTol, warnings);

            var inputs = new List<Region>();
            for (int i = 0; i < slabs.Count; i++)
                inputs.Add(Tessellator.Tessellate(slabs[i], table, reg, options.TessellationTolerance, i));

            if (options.JoinTolerance > 0) SnapAcrossSlabs(inputs, reg, table, options.JoinTolerance, classifyTol);

            var allEdges = new List<(RegistryPoint, RegistryPoint)>();
            var subject = new Paths64();
            foreach (Region r in inputs)
            {
                allEdges.AddRange(r.Edges(reg));
                subject.AddRange(ClipperBridge.ToPaths(r, reg));
            }

            List<(Path64 Outer, List<Path64> Holes)> united = ClipperBridge.Execute(ClipType.Union, subject, null);

            if (united.Count > 1 && options.JoinTolerance > 0)
            {
                // ultimo recurso para microgaps que el ajuste de vertices no cerro
                double delta = options.JoinTolerance / 2 * options.Scale;
                Paths64 inflated = Clipper.InflatePaths(subject, delta, JoinType.Miter, EndType.Polygon, 2.0, 0.0);
                Paths64 deflated = Clipper.InflatePaths(inflated, -delta, JoinType.Miter, EndType.Polygon, 2.0, 0.0);
                List<(Path64 Outer, List<Path64> Holes)> closed = ClipperBridge.Execute(ClipType.Union, deflated, null);
                if (closed.Count < united.Count)
                {
                    united = closed;
                    warnings.Add(new GeometryWarning(WarningKind.GapsClosedByInflate,
                        "Se cerraron microgaps inflando y desinflando " + FormatMm(options.JoinTolerance / 2) +
                        " mm; los bordes afectados pueden haber quedado como polilinea."));
                }
            }

            var regions = new List<Region>();
            foreach ((Path64 outer, List<Path64> holes) in united)
                regions.AddRange(mapper.Map(outer, holes, allEdges));

            Cleanup.CollapseShortEdges(regions, reg, options.ShortCurveTolerance, warnings);
            var rebuilder = new ContourRebuilder(table, reg, classifyTol);
            var result = new List<Polygon2>();
            foreach (Region r in regions)
            {
                Polygon2 p = rebuilder.Rebuild(r);
                if (p == null) continue;
                if (p.Outer.Area / (p.Outer.Perimeter / 2) < options.ShortCurveTolerance) continue;
                if (p.Area < options.MinArea)
                {
                    warnings.Add(new GeometryWarning(WarningKind.SliverDiscarded,
                        string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "Region de area {0:0.####} descartada por ser menor que el umbral {1:0.####}.", p.Area, options.MinArea)));
                    continue;
                }
                result.Add(p);
            }
            if (result.Count > 1)
                warnings.Add(new GeometryWarning(WarningKind.DisconnectedRegions,
                    "Las losas no forman una sola region: se obtienen " + result.Count + " regiones separadas."));
            return new MergeResult(result, warnings);
        }

        private static string FormatMm(double feet) =>
            (feet * 304.8).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// Cierra microgaps: vertices de losas distintas mas cercanos que la tolerancia se
        /// fusionan, y los vertices que caen a menos de la tolerancia de un borde ajeno se
        /// insertan en ese borde (heredando la curva original si la hay).
        /// </summary>
        private static void SnapAcrossSlabs(List<Region> regions, PointRegistry reg, CurveTable table, double tolerance, double classifyTol)
        {
            // 1. vertice - vertice
            var points = new List<RegistryPoint>();
            foreach (Region r in regions)
                foreach (Ring ring in r.AllRings())
                    foreach (RegistryPoint p in ring) points.Add(p);
            foreach (RegistryPoint p in points)
            {
                RegistryPoint a = reg.Resolve(p);
                foreach (RegistryPoint q in new List<RegistryPoint>(reg.Near(a.Position, tolerance)))
                {
                    RegistryPoint b = reg.Resolve(q);
                    if (ReferenceEquals(a, b) || a.Slab == b.Slab) continue;
                    RegistryPoint survivor = a.Kind < b.Kind || (a.Kind == b.Kind && a.Id < b.Id) ? a : b;
                    RegistryPoint victim = ReferenceEquals(survivor, a) ? b : a;
                    reg.Merge(victim, survivor);
                    a = survivor;
                }
            }

            // 2. vertice - borde ajeno
            foreach (Region r in regions)
                foreach (Ring ring in r.AllRings())
                {
                    List<RegistryPoint> pts = Region.ResolvedDistinct(ring, reg);
                    var insertions = new List<(int After, double T, RegistryPoint Point)>();
                    for (int i = 0; i < pts.Count; i++)
                    {
                        RegistryPoint a = pts[i], b = pts[(i + 1) % pts.Count];
                        if (ReferenceEquals(a, b)) continue;
                        var seg = new Segment2(a.Position, b.Position);
                        int curveId = EdgeClassifier.Classify(a, b, table, classifyTol);
                        foreach (RegistryPoint q in reg.Near(seg.PointAt(0.5), seg.Length / 2 + tolerance))
                        {
                            if (ReferenceEquals(q, a) || ReferenceEquals(q, b)) continue;
                            if (q.Slab == a.Slab && q.Slab == b.Slab) continue;
                            double t = seg.ClosestParameter(q.Position);
                            if (seg.PointAt(t).DistanceTo(q.Position) > tolerance) continue;
                            if (q.Position.DistanceTo(a.Position) <= tolerance || q.Position.DistanceTo(b.Position) <= tolerance) continue;
                            if (curveId >= 0)
                            {
                                ICurve2 c = table[curveId];
                                q.AddMembership(curveId, c.ClosestParameter(q.Position));
                            }
                            insertions.Add((i, t, q));
                        }
                    }
                    if (insertions.Count == 0) continue;
                    insertions.Sort((x, y) => x.After != y.After ? x.After.CompareTo(y.After) : x.T.CompareTo(y.T));
                    var rebuilt = new List<RegistryPoint>();
                    int k = 0;
                    for (int i = 0; i < pts.Count; i++)
                    {
                        rebuilt.Add(pts[i]);
                        while (k < insertions.Count && insertions[k].After == i) rebuilt.Add(insertions[k++].Point);
                    }
                    ring.Clear();
                    ring.AddRange(rebuilt);
                }
        }
    }
}
