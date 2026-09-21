using System;
using System.Collections.Generic;
using ARBA.Losas.Geometry.Internal;
using Clipper2Lib;

namespace ARBA.Losas.Geometry
{
    /// <summary>
    /// Divide una region (losa) en varias segun lineas de corte. Cada linea se aplica en
    /// secuencia sobre el conjunto de regiones vigente: por cada region se construyen los
    /// dos semiplanos de la linea y se intersecan con Clipper2. Los huecos se reparten o se
    /// parten con el corte. Al final se reconstruyen los arcos originales y se limpian los
    /// tramos cortos.
    /// </summary>
    public static class SlabSplitter
    {
        public static SplitResult Split(Polygon2 slab, IReadOnlyList<CutLine> cuts, SplitOptions options)
        {
            if (slab == null) throw new ArgumentNullException(nameof(slab));
            if (cuts == null) throw new ArgumentNullException(nameof(cuts));
            options ??= new SplitOptions();
            ValidateOptions(options);

            var warnings = new List<GeometryWarning>();
            var table = new CurveTable();
            var reg = new PointRegistry(options.Scale);
            double classifyTol = EdgeClassifier.Tolerance(options.TessellationTolerance, options.Scale);
            var mapper = new OutputMapper(reg, table, classifyTol, warnings);
            var engine = new SplitEngine(options, reg, table, mapper, warnings);

            var regions = new List<Region> { Tessellator.Tessellate(slab, table, reg, options.TessellationTolerance, 0) };
            var sliverRegions = new List<Region>();

            for (int i = 0; i < cuts.Count; i++)
            {
                var next = new List<Region>();
                bool divided = false;
                int warningsBefore = warnings.Count;
                foreach (Region region in regions)
                {
                    List<Region> pieces = engine.CutRegion(region, cuts[i], i, sliverRegions);
                    if (pieces.Count > 1) divided = true;
                    next.AddRange(pieces);
                }
                bool crossWarned = false;
                for (int w = warningsBefore; w < warnings.Count; w++)
                    if (warnings[w].Kind == WarningKind.CutDoesNotCross) crossWarned = true;
                if (!divided && !crossWarned)
                    warnings.Add(new GeometryWarning(WarningKind.CutDidNotDivide,
                        "La linea de corte " + (i + 1) + " (" + cuts[i] + ") no dividio ninguna region.", i));
                regions = next;
            }

            Cleanup.CollapseShortEdges(regions, reg, options.ShortCurveTolerance, warnings);
            var rebuilder = new ContourRebuilder(table, reg, classifyTol);
            var result = new List<Polygon2>();
            foreach (Region r in regions)
            {
                Polygon2 p = rebuilder.Rebuild(r);
                if (p != null) result.Add(p);
            }
            var slivers = new List<Polygon2>();
            foreach (Region r in sliverRegions)
            {
                Polygon2 p = rebuilder.Rebuild(r);
                if (p != null) slivers.Add(p);
            }
            return new SplitResult(result, slivers, warnings);
        }

        private static void ValidateOptions(SplitOptions o)
        {
            if (!(o.Scale > 0)) throw new ArgumentException("Scale debe ser positivo.");
            if (!(o.TessellationTolerance > 0)) throw new ArgumentException("TessellationTolerance debe ser positiva.");
            if (!(o.ShortCurveTolerance >= 0)) throw new ArgumentException("ShortCurveTolerance no puede ser negativa.");
            if (!(o.MinArea >= 0)) throw new ArgumentException("MinArea no puede ser negativa.");
        }

        /// <summary>Estado y pasos de la division de una region por una linea.</summary>
        private sealed class SplitEngine
        {
            private readonly SplitOptions _o;
            private readonly PointRegistry _reg;
            private readonly CurveTable _table;
            private readonly OutputMapper _mapper;
            private readonly List<GeometryWarning> _warnings;

            public SplitEngine(SplitOptions o, PointRegistry reg, CurveTable table, OutputMapper mapper, List<GeometryWarning> warnings)
            {
                _o = o; _reg = reg; _table = table; _mapper = mapper; _warnings = warnings;
            }

            public List<Region> CutRegion(Region region, CutLine cut, int cutIndex, List<Region> slivers)
            {
                Paths64 subject = ClipperBridge.ToPaths(region, _reg);
                if (subject.Count == 0) return new List<Region>();
                List<(RegistryPoint A, RegistryPoint B)> edges = region.Edges(_reg);

                List<(double S0, double S1)> uncovered = null;
                if (!_o.ExtendCutLines)
                {
                    List<(double S0, double S1)> chords = Chords(region, subject, cut);
                    if (chords.Count == 0) return new List<Region> { region };
                    var covered = new List<(double, double)>();
                    uncovered = new List<(double, double)>();
                    double tol = _o.ShortCurveTolerance;
                    foreach ((double s0, double s1) in chords)
                    {
                        if (s0 >= -tol && s1 <= cut.Length + tol) covered.Add((s0, s1));
                        else uncovered.Add((s0, s1));
                    }
                    if (covered.Count == 0)
                    {
                        _warnings.Add(new GeometryWarning(WarningKind.CutDoesNotCross,
                            "La linea de corte " + (cutIndex + 1) + " no cruza la region de lado a lado; se ignora en esa region.", cutIndex));
                        return new List<Region> { region };
                    }
                }

                HalfPlanes(subject, cut, out Path64 left, out Path64 right);
                var pieces = new List<(Path64 Outer, List<Path64> Holes)>();
                pieces.AddRange(ClipperBridge.Execute(ClipType.Intersection, subject, new Paths64 { left }));
                pieces.AddRange(ClipperBridge.Execute(ClipType.Intersection, subject, new Paths64 { right }));

                var regions = new List<Region>();
                foreach ((Path64 outer, List<Path64> holes) in pieces)
                    regions.AddRange(_mapper.Map(outer, holes, edges));

                if (uncovered != null && uncovered.Count > 0 && regions.Count > 1)
                    regions = RejoinAcrossUncoveredChords(regions, cut, uncovered, edges);

                return FilterSlivers(regions, cutIndex, slivers);
            }

            /// <summary>Descarta ruido numerico (mas fino que la tolerancia de curva corta) y regiones con area menor al umbral.</summary>
            private List<Region> FilterSlivers(List<Region> regions, int cutIndex, List<Region> slivers)
            {
                var kept = new List<Region>();
                foreach (Region r in regions)
                {
                    Paths64 paths = ClipperBridge.ToPaths(r, _reg);
                    if (paths.Count == 0) continue;
                    var holes = new List<Path64>();
                    for (int i = 1; i < paths.Count; i++) holes.Add(paths[i]);
                    double area = ClipperBridge.Area(paths[0], holes, _o.Scale);
                    double perimeter = ClipperBridge.Perimeter(paths[0], holes, _o.Scale);
                    if (perimeter <= 0 || area / (perimeter / 2) < _o.ShortCurveTolerance) continue; // ruido: mas fino que la curva corta
                    if (area < _o.MinArea)
                    {
                        slivers.Add(r);
                        _warnings.Add(new GeometryWarning(WarningKind.SliverDiscarded,
                            string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                "Region de area {0:0.####} descartada tras la linea de corte {1} por ser menor que el umbral {2:0.####}.",
                                area, cutIndex + 1, _o.MinArea), cutIndex));
                        continue;
                    }
                    kept.Add(r);
                }
                return kept;
            }

            /// <summary>Dos rectangulos que cubren la region a cada lado de la recta.</summary>
            private void HalfPlanes(Paths64 subject, CutLine cut, out Path64 left, out Path64 right)
            {
                Rect64 b = Clipper.GetBounds(subject);
                double scale = _o.Scale;
                Vec2 d = cut.Direction, n = d.Perpendicular();
                double sMin = double.MaxValue, sMax = double.MinValue, nMin = double.MaxValue, nMax = double.MinValue;
                foreach (Point64 c in new[] { new Point64(b.left, b.top), new Point64(b.right, b.top), new Point64(b.right, b.bottom), new Point64(b.left, b.bottom) })
                {
                    Vec2 p = new Vec2(c.X / scale, c.Y / scale);
                    double s = cut.ProjectParameter(p), h = cut.SignedDistance(p);
                    sMin = Math.Min(sMin, s); sMax = Math.Max(sMax, s);
                    nMin = Math.Min(nMin, h); nMax = Math.Max(nMax, h);
                }
                double margin = Math.Max(sMax - sMin, nMax - nMin) + 1;
                sMin -= margin; sMax += margin;
                double far = Math.Max(Math.Abs(nMin), Math.Abs(nMax)) + margin;
                Point64 K(double s, double h) => _reg.ToKey(cut.A + d * s + n * h);
                left = new Path64 { K(sMin, 0), K(sMax, 0), K(sMax, far), K(sMin, far) };
                right = new Path64 { K(sMin, -far), K(sMax, -far), K(sMax, 0), K(sMin, 0) };
            }

            /// <summary>Intervalos (parametro sobre la recta) en los que la recta esta dentro de la region.</summary>
            private List<(double S0, double S1)> Chords(Region region, Paths64 subject, CutLine cut)
            {
                var crossings = new List<double>();
                foreach ((RegistryPoint a, RegistryPoint b) in region.Edges(_reg))
                {
                    double da = cut.SignedDistance(a.Position), db = cut.SignedDistance(b.Position);
                    double eps = 1e-12;
                    if (Math.Abs(da) <= eps) crossings.Add(cut.ProjectParameter(a.Position));
                    if (Math.Abs(db) <= eps) crossings.Add(cut.ProjectParameter(b.Position));
                    if ((da > eps && db < -eps) || (da < -eps && db > eps))
                    {
                        double t = da / (da - db);
                        crossings.Add(cut.ProjectParameter(a.Position + (b.Position - a.Position) * t));
                    }
                }
                crossings.Sort();
                var distinct = new List<double>();
                double dedupe = 2.0 / _o.Scale;
                foreach (double s in crossings)
                    if (distinct.Count == 0 || s - distinct[distinct.Count - 1] > dedupe) distinct.Add(s);

                var holes = new List<Path64>();
                for (int i = 1; i < subject.Count; i++) holes.Add(subject[i]);
                var chords = new List<(double, double)>();
                for (int i = 0; i + 1 < distinct.Count; i++)
                {
                    double s0 = distinct[i], s1 = distinct[i + 1];
                    Point64 mid = _reg.ToKey(cut.PointAtParameter((s0 + s1) / 2));
                    if (ClipperBridge.Contains(subject[0], holes, mid)) chords.Add((s0, s1));
                }
                return chords;
            }

            /// <summary>
            /// Modo sin extender: las piezas que solo se separaron a lo largo de una cuerda que la
            /// linea finita no cubre se vuelven a unir.
            /// </summary>
            private List<Region> RejoinAcrossUncoveredChords(List<Region> regions, CutLine cut, List<(double S0, double S1)> uncovered,
                                                              List<(RegistryPoint A, RegistryPoint B)> edges)
            {
                double lineTol = _o.TessellationTolerance * 1.5 + 4.0 / _o.Scale;
                int n = regions.Count;
                var parent = new int[n];
                for (int i = 0; i < n; i++) parent[i] = i;
                int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }

                // intervalos de cada pieza sobre la recta de corte
                var intervals = new List<List<(double, double)>>();
                foreach (Region r in regions)
                {
                    var list = new List<(double, double)>();
                    foreach ((RegistryPoint a, RegistryPoint b) in r.Edges(_reg))
                    {
                        if (Math.Abs(cut.SignedDistance(a.Position)) > lineTol || Math.Abs(cut.SignedDistance(b.Position)) > lineTol) continue;
                        double sa = cut.ProjectParameter(a.Position), sb = cut.ProjectParameter(b.Position);
                        list.Add((Math.Min(sa, sb), Math.Max(sa, sb)));
                    }
                    intervals.Add(list);
                }
                foreach ((double c0, double c1) in uncovered)
                {
                    var touching = new List<int>();
                    for (int i = 0; i < n; i++)
                        foreach ((double a, double b) in intervals[i])
                            if (Math.Min(b, c1) - Math.Max(a, c0) > _o.ShortCurveTolerance) { touching.Add(i); break; }
                    for (int k = 1; k < touching.Count; k++) parent[Find(touching[k])] = Find(touching[0]);
                }

                var groups = new Dictionary<int, List<Region>>();
                for (int i = 0; i < n; i++)
                {
                    int root = Find(i);
                    if (!groups.TryGetValue(root, out var g)) groups[root] = g = new List<Region>();
                    g.Add(regions[i]);
                }
                var result = new List<Region>();
                foreach (List<Region> g in groups.Values)
                {
                    if (g.Count == 1) { result.Add(g[0]); continue; }
                    var subject = new Paths64();
                    foreach (Region r in g) subject.AddRange(ClipperBridge.ToPaths(r, _reg));
                    foreach ((Path64 outer, List<Path64> holes) in ClipperBridge.Execute(ClipType.Union, subject, null))
                        result.AddRange(_mapper.Map(outer, holes, edges));
                }
                return result;
            }
        }
    }
}
