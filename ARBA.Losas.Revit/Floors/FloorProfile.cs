using System.Collections.Generic;
using ARBA.Losas.Geometry;
using Autodesk.Revit.DB;

namespace ARBA.Losas.Revit.Floors
{
    public enum ProfileSource
    {
        /// <summary>Sketch.Profile: curvas originales del boceto, con los arcos sin teselar.</summary>
        Sketch,
        /// <summary>HostObjectUtils.GetTopFaces + PlanarFace.GetEdgesAsCurveLoops (la losa no tenia boceto accesible).</summary>
        TopFace
    }

    /// <summary>Curva de Revit de la que procede una curva del Polygon2, o null si esa curva es un tramo teselado.</summary>
    public sealed class SourceCurve
    {
        public Curve Curve { get; }
        /// <summary>True si el Polygon2 recorre la curva de Revit en sentido contrario (se invirtio para orientar el contorno).</summary>
        public bool Reversed { get; }

        public SourceCurve(Curve curve, bool reversed)
        {
            Curve = curve;
            Reversed = reversed;
        }
    }

    /// <summary>Contorno de una losa traducido a la geometria pura, con la trazabilidad a las curvas de Revit.</summary>
    public sealed class FloorProfile
    {
        public Floor Floor { get; }
        public Polygon2 Polygon { get; }
        /// <summary>Una entrada por curva de <see cref="Polygon"/> (exterior y luego huecos, en orden), o null si fue teselada.</summary>
        public IReadOnlyList<SourceCurve> Sources { get; }
        public FloorPlane Plane { get; }
        public ProfileSource Source { get; }
        public IReadOnlyList<string> Warnings { get; }

        public FloorProfile(Floor floor, Polygon2 polygon, IReadOnlyList<SourceCurve> sources, FloorPlane plane, ProfileSource source, IReadOnlyList<string> warnings)
        {
            Floor = floor;
            Polygon = polygon;
            Sources = sources;
            Plane = plane;
            Source = source;
            Warnings = warnings;
        }
    }
}
