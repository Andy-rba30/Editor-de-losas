using System;
using System.Collections.Generic;
using ARBA.Losas.Geometry;
using ARBA.Losas.Revit.Floors;
using ARBA.Losas.Revit.Operations;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace ARBA.Losas.Revit.Selection
{
    /// <summary>Obtiene las lineas de corte, dibujadas al vuelo o seleccionadas, ya proyectadas al plano de la losa.</summary>
    public static class CutLinePicker
    {
        private const ObjectSnapTypes Snaps = ObjectSnapTypes.Endpoints | ObjectSnapTypes.Midpoints | ObjectSnapTypes.Intersections |
                                              ObjectSnapTypes.Perpendicular | ObjectSnapTypes.Centers | ObjectSnapTypes.Quadrants |
                                              ObjectSnapTypes.Nearest | ObjectSnapTypes.WorkPlaneGrid;

        /// <summary>
        /// Bucle de PickPoint encadenando puntos (cada punto nuevo cierra una linea con el
        /// anterior) hasta Esc. El conteo va en la barra de estado; ningun dialogo dentro del bucle.
        /// </summary>
        public static List<CutLine> Draw(UIDocument uidoc, FloorPlane plane, List<string> warnings)
        {
            var lines = new List<CutLine>();
            Autodesk.Revit.UI.Selection.Selection sel = uidoc.Selection;
            XYZ previous = null;
            try
            {
                while (true)
                {
                    string prompt = previous == null
                        ? "Linea de corte " + (lines.Count + 1) + ": primer punto (Esc para terminar; " + lines.Count + " lineas)"
                        : "Linea de corte " + (lines.Count + 1) + ": siguiente punto (Esc para terminar; " + lines.Count + " lineas)";
                    XYZ p = sel.PickPoint(Snaps, prompt);
                    if (previous != null)
                    {
                        Vec2 a = plane.To2D(previous), b = plane.To2D(p);
                        if (a.DistanceTo(b) > 1e-9) lines.Add(new CutLine(a, b));
                        else warnings.Add("Se ignoro una linea de corte de longitud cero.");
                    }
                    previous = p;
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Esc: fin del dibujo
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException ex)
            {
                throw new AbortException("No se pueden marcar puntos en esta vista (hace falta un plano de trabajo, por ejemplo una vista de planta): " + ex.Message);
            }
            return lines;
        }

        /// <summary>PickObjects de lineas de modelo, de detalle y rejillas. Solo se admiten rectas; el resto se avisa y se omite.</summary>
        public static List<CutLine> Select(UIDocument uidoc, FloorPlane plane, List<string> warnings)
        {
            var lines = new List<CutLine>();
            IList<Reference> refs;
            try
            {
                refs = uidoc.Selection.PickObjects(ObjectType.Element, new CutCurveSelectionFilter(),
                    "Selecciona las lineas de corte (lineas de modelo, de detalle o rejillas) y pulsa Finalizar");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return lines;
            }
            foreach (Reference r in refs)
            {
                Element e = uidoc.Document.GetElement(r);
                Curve curve = e switch
                {
                    CurveElement ce => ce.GeometryCurve,
                    Grid g => g.Curve,
                    _ => null
                };
                if (!(curve is Line line))
                {
                    warnings.Add("El elemento " + e.Id.Value + " no es una recta (" + (curve?.GetType().Name ?? "sin curva") + "): se omite como linea de corte.");
                    continue;
                }
                Vec2 a = plane.To2D(line.GetEndPoint(0)), b = plane.To2D(line.GetEndPoint(1));
                if (a.DistanceTo(b) <= 1e-9)
                {
                    warnings.Add("El elemento " + e.Id.Value + " es vertical (se proyecta a un punto): se omite.");
                    continue;
                }
                lines.Add(new CutLine(a, b));
            }
            return lines;
        }
    }
}
