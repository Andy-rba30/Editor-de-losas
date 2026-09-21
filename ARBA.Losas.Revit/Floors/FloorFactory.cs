using System.Collections.Generic;
using ARBA.Losas.Geometry;
using Autodesk.Revit.DB;

namespace ARBA.Losas.Revit.Floors
{
    /// <summary>Crea una losa con Floor.Create a partir de una region, validando los loops antes de llamar.</summary>
    public static class FloorFactory
    {
        public static Floor Create(Document doc, Polygon2 region, IReadOnlyList<FloorProfile> profiles, Floor template, List<string> warnings)
        {
            FloorPlane plane = profiles[0].Plane;
            IList<CurveLoop> loops = CurveLoopConverter.ToCurveLoops(region, profiles, plane, warnings);
            foreach (CurveLoop loop in loops)
            {
                if (loop.IsOpen()) throw new System.InvalidOperationException("Loop abierto al crear la losa.");
                if (!loop.HasPlane()) throw new System.InvalidOperationException("Loop no plano al crear la losa.");
            }
            bool structural = FloorInspector.IsStructural(template);
            return Floor.Create(doc, loops, template.FloorType.Id, template.LevelId, structural, null, 0.0);
        }
    }
}
