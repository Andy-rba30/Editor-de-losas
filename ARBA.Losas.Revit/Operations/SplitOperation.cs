using System;
using System.Collections.Generic;
using System.Linq;
using ARBA.Losas.Geometry;
using ARBA.Losas.Revit.Floors;
using ARBA.Losas.Revit.Selection;
using ARBA.Losas.Revit.Settings;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace ARBA.Losas.Revit.Operations
{
    /// <summary>
    /// Flujo completo de Dividir Losa: seleccion, validacion, contorno, lineas de corte,
    /// subdivision en la geometria pura, creacion de losas, parametros, barras, uniones y
    /// borrado de la original, todo dentro de un TransactionGroup que se asimila al final.
    /// Devuelve null si el usuario cancelo antes de tocar el modelo.
    /// </summary>
    public static class SplitOperation
    {
        public static OperationSummary Run(UIDocument uidoc, LosasSettings settings)
        {
            Document doc = uidoc.Document;
            var summary = new OperationSummary { Title = "Dividir losa" };

            // 1. seleccion
            Floor floor;
            try
            {
                Reference r = uidoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element, new FloorSelectionFilter(), "Selecciona la losa a dividir");
                floor = doc.GetElement(r) as Floor;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return null; }
            if (floor == null) throw new AbortException("El elemento seleccionado no es una losa.");

            // 2. condiciones de aborto y contorno
            FloorInspector.Validate(floor);
            FloorProfile profile = FloorProfileReader.Read(floor);
            summary.Warnings.AddRange(profile.Warnings);

            // 3. acero y dependientes
            DependentReport deps = DependentsInspector.Inspect(floor);
            summary.RebarsTotal = deps.Rebars.Count;
            if (deps.Rebars.Count > 0 && !settings.KeepRebar)
                throw new AbortException("La losa " + floor.Id.Value + " tiene " + deps.Rebars.Count + " barras. Divide antes de armar o elimina el acero " +
                                         "(o activa \"Conservar barras\" para re-alojarlas en las losas nuevas).");
            if (settings.DeleteOriginal) summary.Warnings.AddRange(deps.LossWarnings());

            // 4. lineas de corte
            List<CutLine> cuts = settings.CutLineMode == CutLineMode.Draw
                ? CutLinePicker.Draw(uidoc, profile.Plane, summary.Warnings)
                : CutLinePicker.Select(uidoc, profile.Plane, summary.Warnings);
            if (cuts.Count == 0) throw new AbortException("No se indico ninguna linea de corte.");

            // 5-6. subdivision y reconstruccion en la geometria pura
            double shortCurve = uidoc.Application.Application.ShortCurveTolerance;
            SplitResult split = SlabSplitter.Split(profile.Polygon, cuts, settings.ToSplitOptions(shortCurve));
            foreach (GeometryWarning w in split.Warnings)
                if (w.Kind != WarningKind.ShortEdgeCollapsed) summary.Warnings.Add(w.Message);
            if (split.Regions.Count < 2)
                throw new AbortException("Las lineas de corte no dividen la losa " + floor.Id.Value + " en mas de una region." +
                                         (summary.Warnings.Count > 0 ? "\n\n" + string.Join("\n", summary.Warnings) : ""));

            // 7-12. cambios en el modelo
            var profiles = new List<FloorProfile> { profile };
            HashSet<ElementId> joined = settings.KeepJoins ? JoinManager.CollectJoined(doc, new[] { floor }) : new HashSet<ElementId>();
            var revitWarnings = new List<string>();

            using (var group = new TransactionGroup(doc, "ARBA: Dividir losa " + floor.Id.Value))
            {
                group.Start();
                var created = new List<(Floor Floor, Polygon2 Region)>();
                try
                {
                    using (var t = new Transaction(doc, "Crear losas"))
                    {
                        OverlapWarningPreprocessor.Apply(t, !settings.DeleteOriginal, revitWarnings);
                        t.Start();
                        int index = 0;
                        foreach (Polygon2 region in split.Regions)
                        {
                            index++;
                            Floor f;
                            try { f = FloorFactory.Create(doc, region, profiles, floor, summary.Warnings); }
                            catch (Exception ex) when (ex is Autodesk.Revit.Exceptions.ApplicationException || ex is InvalidOperationException)
                            {
                                throw new AbortException("No se pudo crear la losa de la region " + index + " (area " +
                                                         FormatArea(region.Area) + "): " + ex.Message);
                            }
                            if (settings.CopyParameters) ParameterCopier.Copy(floor, f, index, summary.Warnings);
                            created.Add((f, region));
                        }
                        doc.Regenerate();
                        if (settings.KeepRebar && deps.Rebars.Count > 0)
                            summary.RebarsRehosted = RebarRehoster.Rehost(doc, deps.Rebars, created, profile.Plane, summary.Warnings);
                        t.Commit();
                    }

                    if (settings.DeleteOriginal)
                    {
                        using (var t = new Transaction(doc, "Eliminar losa original"))
                        {
                            OverlapWarningPreprocessor.Apply(t, false, revitWarnings);
                            t.Start();
                            doc.Delete(floor.Id);
                            summary.Deleted.Add(floor.Id);
                            t.Commit();
                        }
                    }
                    else
                    {
                        summary.Warnings.Add("Se conserva la losa original " + floor.Id.Value + ": las losas nuevas se superponen a ella.");
                    }

                    if (settings.KeepJoins && joined.Count > 0)
                    {
                        using (var t = new Transaction(doc, "Restablecer uniones"))
                        {
                            OverlapWarningPreprocessor.Apply(t, !settings.DeleteOriginal, revitWarnings);
                            t.Start();
                            summary.JoinsRestored = JoinManager.Rejoin(doc, created.Select(c => c.Floor), joined, summary.Warnings);
                            t.Commit();
                        }
                    }
                }
                catch
                {
                    group.RollBack();
                    throw;
                }
                group.Assimilate();
                foreach ((Floor f, Polygon2 _) in created) summary.Created.Add(f.Id);
            }

            foreach (Polygon2 s in split.DiscardedSlivers)
                summary.Warnings.Add("Region descartada por area minima: " + FormatArea(s.Area) + ".");
            summary.Warnings.AddRange(revitWarnings.Distinct());
            return summary;
        }

        public static string FormatArea(double squareFeet) =>
            (squareFeet / LosasSettings.SquareFeetPerSquareMeter).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " m²";
    }
}
