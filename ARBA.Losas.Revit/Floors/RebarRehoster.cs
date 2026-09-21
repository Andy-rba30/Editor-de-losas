using System;
using System.Collections.Generic;
using ARBA.Losas.Geometry;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace ARBA.Losas.Revit.Floors
{
    /// <summary>
    /// Re-aloja cada barra en la losa nueva que contiene el centro de su caja envolvente
    /// (Rebar.SetHostId), sin tocar su geometria. Las que no se puedan re-alojar quedan en
    /// las advertencias con su ElementId. El armado no se recorta ni se regenera: el flujo
    /// ARBA es dividir primero y armar despues con Acero-automatico.
    /// </summary>
    public static class RebarRehoster
    {
        public static int Rehost(Document doc, IEnumerable<Rebar> rebars, IReadOnlyList<(Floor Floor, Polygon2 Region)> targets, FloorPlane plane, List<string> warnings)
        {
            int done = 0;
            var valid = new List<(Floor Floor, Polygon2 Region)>();
            foreach ((Floor floor, Polygon2 region) in targets)
            {
                if (RebarHostData.IsValidHost(floor)) valid.Add((floor, region));
                else warnings.Add("La losa nueva " + floor.Id.Value + " no puede alojar barras (no es estructural o su material no es hormigon).");
            }
            foreach (Rebar rebar in rebars)
            {
                long id = rebar.Id.Value;
                BoundingBoxXYZ box = rebar.get_BoundingBox(null);
                if (box == null)
                {
                    warnings.Add("La barra " + id + " no tiene caja envolvente: no se pudo re-alojar.");
                    continue;
                }
                XYZ center3 = (box.Min + box.Max) * 0.5;
                if (box.Transform != null) center3 = box.Transform.OfPoint(center3);
                Vec2 center = plane.To2D(center3);

                Floor target = null;
                foreach ((Floor floor, Polygon2 region) in valid)
                    if (region.Contains(center)) { target = floor; break; }
                if (target == null)
                {
                    warnings.Add("El centro de la barra " + id + " (" + Fmt(center) + ") no cae dentro de ninguna losa nueva valida: no se pudo re-alojar.");
                    continue;
                }
                try
                {
                    rebar.SetHostId(doc, target.Id);
                    done++;
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException ex)
                {
                    warnings.Add("La barra " + id + " no se pudo re-alojar en la losa " + target.Id.Value + ": " + ex.Message);
                }
            }
            return done;
        }

        private static string Fmt(Vec2 p) =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.###}, {1:0.###}", p.X, p.Y);
    }
}
