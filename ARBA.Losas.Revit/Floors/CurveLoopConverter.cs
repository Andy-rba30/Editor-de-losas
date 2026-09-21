using System;
using System.Collections.Generic;
using ARBA.Losas.Geometry;
using ARBA.Losas.Revit.Operations;
using Autodesk.Revit.DB;

namespace ARBA.Losas.Revit.Floors
{
    /// <summary>
    /// Frontera entre Revit y la geometria pura: CurveLoop (o CurveArray) a Polygon2 al
    /// entrar y Polygon2 a CurveLoop al salir. Lineas y arcos se traducen exactos; elipses,
    /// splines y cualquier otra curva se teselan como polilinea y se avisa.
    /// </summary>
    public static class CurveLoopConverter
    {
        /// <summary>
        /// Construye el Polygon2 a partir de los loops (cualquier orden): el de mayor area es el
        /// exterior. Los contornos se orientan aqui (exterior antihorario, huecos horarios) para
        /// que <paramref name="sources"/> quede alineado curva a curva con el poligono.
        /// </summary>
        public static Polygon2 ToPolygon(IList<IList<Curve>> loops, FloorPlane plane, out List<SourceCurve> sources, List<string> warnings)
        {
            var contours = new List<(Contour Contour, List<SourceCurve> Sources)>();
            foreach (IList<Curve> loop in loops)
            {
                (Contour c, List<SourceCurve> s) = ToContour(loop, plane, warnings);
                if (c != null) contours.Add((c, s));
            }
            if (contours.Count == 0) throw new InvalidOperationException("El boceto no tiene ningun loop valido.");

            int outer = 0;
            for (int i = 1; i < contours.Count; i++)
                if (contours[i].Contour.Area > contours[outer].Contour.Area) outer = i;

            sources = new List<SourceCurve>();
            (Contour outerContour, List<SourceCurve> outerSources) = Oriented(contours[outer].Contour, contours[outer].Sources, true);
            sources.AddRange(outerSources);
            var holes = new List<Contour>();
            for (int i = 0; i < contours.Count; i++)
            {
                if (i == outer) continue;
                (Contour h, List<SourceCurve> hs) = Oriented(contours[i].Contour, contours[i].Sources, false);
                holes.Add(h);
                sources.AddRange(hs);
            }
            return new Polygon2(outerContour, holes);
        }

        private static (Contour, List<SourceCurve>) Oriented(Contour contour, List<SourceCurve> sources, bool counterClockwise)
        {
            if (contour.IsCounterClockwise == counterClockwise) return (contour, sources);
            var reversedSources = new List<SourceCurve>(sources.Count);
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                SourceCurve s = sources[i];
                reversedSources.Add(s == null ? null : new SourceCurve(s.Curve, !s.Reversed));
            }
            return (contour.Reversed(), reversedSources);
        }

        /// <summary>Tolerancia para considerar que el fin de una curva coincide con el inicio de la siguiente.</summary>
        private const double ChainTolerance = 1e-6;

        private static (Contour, List<SourceCurve>) ToContour(IList<Curve> loop, FloorPlane plane, List<string> warnings)
        {
            var curves = new List<ICurve2>();
            var sources = new List<SourceCurve>();
            foreach ((Curve c, bool reversed) in Chain(loop, plane))
            {
                XYZ p0 = c.GetEndPoint(reversed ? 1 : 0), p1 = c.GetEndPoint(reversed ? 0 : 1);
                if (c is Line)
                {
                    Vec2 a = plane.To2D(p0), b = plane.To2D(p1);
                    if (a == b) continue;
                    curves.Add(new Segment2(a, b));
                    sources.Add(new SourceCurve(c, reversed));
                }
                else if (c is Arc arc)
                {
                    Vec2 a = plane.To2D(p0), b = plane.To2D(p1);
                    Vec2 m = plane.To2D(arc.Evaluate(0.5, true));
                    curves.Add(Arc2.FromThreePoints(a, m, b));
                    sources.Add(new SourceCurve(c, reversed));
                }
                else
                {
                    // elipse, spline, helice...: polilinea de Revit (Curve.Tessellate) y aviso
                    var pts = new List<XYZ>(c.Tessellate());
                    if (reversed) pts.Reverse();
                    int added = 0;
                    for (int i = 0; i + 1 < pts.Count; i++)
                    {
                        Vec2 a = plane.To2D(pts[i]), b = plane.To2D(pts[i + 1]);
                        if (a == b) continue;
                        curves.Add(new Segment2(a, b));
                        sources.Add(null);
                        added++;
                    }
                    warnings.Add("La curva " + c.GetType().Name + " del boceto no es linea ni arco: ese borde queda como polilinea de " + added + " tramos.");
                }
            }
            if (curves.Count < 2 && !(curves.Count == 1 && curves[0] is Arc2 full && full.IsFullCircle)) return (null, null);
            return (new Contour(curves), sources);
        }

        /// <summary>
        /// Ordena las curvas del loop cabeza-cola (Sketch.Profile no garantiza el sentido de cada
        /// curva) e indica cuales hay que recorrer al reves. Lanza si el loop no cierra.
        /// </summary>
        private static List<(Curve Curve, bool Reversed)> Chain(IList<Curve> loop, FloorPlane plane)
        {
            var result = new List<(Curve, bool)>();
            if (loop.Count == 0) return result;
            var pending = new List<Curve>(loop);
            Curve first = pending[0];
            pending.RemoveAt(0);
            result.Add((first, false));
            Vec2 start = plane.To2D(first.GetEndPoint(0));
            Vec2 current = plane.To2D(first.GetEndPoint(1));
            while (pending.Count > 0)
            {
                int best = -1;
                bool reversed = false;
                double bestDist = double.MaxValue;
                for (int i = 0; i < pending.Count; i++)
                {
                    double d0 = plane.To2D(pending[i].GetEndPoint(0)).DistanceTo(current);
                    double d1 = plane.To2D(pending[i].GetEndPoint(1)).DistanceTo(current);
                    if (d0 < bestDist) { bestDist = d0; best = i; reversed = false; }
                    if (d1 < bestDist) { bestDist = d1; best = i; reversed = true; }
                }
                if (bestDist > ChainTolerance)
                    throw new AbortException("El boceto de la losa tiene un loop que no cierra: hay un salto de " +
                                             (bestDist * 304.8).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " mm entre curvas.");
                Curve next = pending[best];
                pending.RemoveAt(best);
                result.Add((next, reversed));
                current = plane.To2D(next.GetEndPoint(reversed ? 0 : 1));
            }
            if (current.DistanceTo(start) > ChainTolerance)
                throw new AbortException("El boceto de la losa tiene un loop abierto: el ultimo punto queda a " +
                                         (current.DistanceTo(start) * 304.8).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " mm del primero.");
            return result;
        }

        /// <summary>Loops de Revit (exterior y huecos) para Floor.Create, con los arcos originales recortados con MakeBound cuando es posible.</summary>
        public static IList<CurveLoop> ToCurveLoops(Polygon2 polygon, IReadOnlyList<FloorProfile> profiles, FloorPlane plane, List<string> warnings)
        {
            var loops = new List<CurveLoop>();
            foreach (Contour contour in polygon.AllContours())
            {
                var revitCurves = new List<Curve>();
                foreach (ICurve2 c in contour.Curves)
                {
                    Curve rc = ToRevitCurve(c, profiles, plane, warnings);
                    if (rc != null) revitCurves.Add(rc);
                }
                CurveLoop loop = CurveLoop.Create(revitCurves);
                if (loop.IsOpen()) throw new InvalidOperationException("El contorno reconstruido no esta cerrado.");
                if (!loop.HasPlane()) throw new InvalidOperationException("El contorno reconstruido no es plano.");
                loops.Add(loop);
            }
            return loops;
        }

        private static Curve ToRevitCurve(ICurve2 c, IReadOnlyList<FloorProfile> profiles, FloorPlane plane, List<string> warnings)
        {
            XYZ start = plane.To3D(c.Start), end = plane.To3D(c.End);
            if (c is Segment2)
                return Line.CreateBound(start, end);

            var arc2 = (Arc2)c;
            XYZ mid = plane.To3D(arc2.MidPoint);
            // Ruta 1: la curva original de Revit recortada con MakeBound (misma definicion, mismo centro y radio)
            SourceCurve source = FindSource(c.Origin, profiles);
            if (source != null && source.Curve is Arc original)
            {
                Curve trimmed = TrimOriginal(original, source.Reversed, c.Origin);
                if (trimmed != null &&
                    trimmed.GetEndPoint(0).IsAlmostEqualTo(start, 1e-6) &&
                    trimmed.GetEndPoint(1).IsAlmostEqualTo(end, 1e-6))
                    return trimmed;
                warnings.Add("El arco original " + c.Origin + " no pudo recortarse con MakeBound; se recrea por tres puntos.");
            }
            // Ruta 2: arco nuevo por centro y angulos (circunferencia completa) o por tres puntos
            if (arc2.IsFullCircle)
            {
                XYZ center = plane.To3D(arc2.Center);
                double a0 = arc2.StartAngle, a1 = arc2.StartAngle + arc2.SweepAngle;
                return Arc.Create(center, arc2.Radius, Math.Min(a0, a1), Math.Max(a0, a1), XYZ.BasisX, XYZ.BasisY);
            }
            return Arc.Create(start, end, mid);
        }

        private static SourceCurve FindSource(CurveOrigin origin, IReadOnlyList<FloorProfile> profiles)
        {
            if (origin == null || origin.Slab < 0 || origin.Slab >= profiles.Count) return null;
            IReadOnlyList<SourceCurve> sources = profiles[origin.Slab].Sources;
            if (origin.Curve < 0 || origin.Curve >= sources.Count) return null;
            return sources[origin.Curve];
        }

        /// <summary>Clona el arco original y lo acota entre los parametros normalizados del origen (invertido si hace falta).</summary>
        private static Curve TrimOriginal(Arc original, bool sourceReversed, CurveOrigin origin)
        {
            try
            {
                double t0 = origin.T0, t1 = origin.T1;
                if (sourceReversed) { t0 = 1 - t0; t1 = 1 - t1; }
                if (t0 == t1) return null;
                Curve clone = original.Clone();
                double lo = Math.Min(t0, t1), hi = Math.Max(t0, t1);
                if (lo > 0 || hi < 1)
                    clone.MakeBound(original.ComputeRawParameter(lo), original.ComputeRawParameter(hi));
                return t0 < t1 ? clone : clone.CreateReversed();
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                return null;
            }
        }
    }
}
