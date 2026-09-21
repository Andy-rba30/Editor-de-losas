using System;
using System.Collections.Generic;
using ARBA.Losas.Geometry;
using ARBA.Losas.Revit.Operations;
using Autodesk.Revit.DB;

namespace ARBA.Losas.Revit.Floors
{
    /// <summary>
    /// Lee el contorno de una losa. Ruta principal: doc.GetElement(floor.SketchId) as Sketch y su
    /// Profile (curvas originales, arcos sin teselar). Si no hay boceto accesible, ruta de
    /// respaldo: HostObjectUtils.GetTopFaces y PlanarFace.GetEdgesAsCurveLoops. La ruta usada
    /// queda registrada en FloorProfile.Source. (No existe floor.GetSketch(); no se usa.)
    /// </summary>
    public static class FloorProfileReader
    {
        public static FloorProfile Read(Floor floor)
        {
            Document doc = floor.Document;
            var warnings = new List<string>();
            IList<IList<Curve>> loops = null;
            ProfileSource source = ProfileSource.Sketch;

            Sketch sketch = floor.SketchId != null && floor.SketchId != ElementId.InvalidElementId
                ? doc.GetElement(floor.SketchId) as Sketch
                : null;
            if (sketch != null)
            {
                loops = new List<IList<Curve>>();
                foreach (CurveArray array in sketch.Profile)
                {
                    var loop = new List<Curve>();
                    foreach (Curve c in array) loop.Add(c);
                    if (loop.Count > 0) loops.Add(loop);
                }
                if (loops.Count == 0) loops = null;
            }

            if (loops == null)
            {
                source = ProfileSource.TopFace;
                loops = ReadFromTopFace(floor);
                warnings.Add("La losa " + floor.Id.Value + " no tiene boceto accesible: el contorno se leyo de su cara superior.");
            }

            double z = PlaneZ(loops);
            var plane = new FloorPlane(z);
            Polygon2 polygon = CurveLoopConverter.ToPolygon(loops, plane, out List<SourceCurve> sources, warnings);
            return new FloorProfile(floor, polygon, sources, plane, source, warnings);
        }

        private static IList<IList<Curve>> ReadFromTopFace(Floor floor)
        {
            IList<Reference> tops;
            try { tops = HostObjectUtils.GetTopFaces(floor); }
            catch (Autodesk.Revit.Exceptions.ApplicationException ex)
            {
                throw new AbortException("No se pudo obtener la cara superior de la losa " + floor.Id.Value + ": " + ex.Message);
            }
            if (tops.Count != 1)
                throw new AbortException("La losa " + floor.Id.Value + " tiene " + tops.Count + " caras superiores; solo se admite una (sin boceto accesible).");
            if (!(floor.GetGeometryObjectFromReference(tops[0]) is PlanarFace face))
                throw new AbortException("La cara superior de la losa " + floor.Id.Value + " no es plana.");
            var loops = new List<IList<Curve>>();
            foreach (CurveLoop loop in face.GetEdgesAsCurveLoops())
            {
                var l = new List<Curve>();
                foreach (Curve c in loop) l.Add(c);
                loops.Add(l);
            }
            return loops;
        }

        /// <summary>Z del plano: la de las curvas del boceto (todas comparten plano en una losa horizontal).</summary>
        private static double PlaneZ(IList<IList<Curve>> loops)
        {
            foreach (IList<Curve> loop in loops)
                foreach (Curve c in loop)
                    return c.GetEndPoint(0).Z;
            throw new InvalidOperationException("Boceto vacio.");
        }
    }
}
