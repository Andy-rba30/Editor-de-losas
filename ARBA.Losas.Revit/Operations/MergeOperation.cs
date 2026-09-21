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
    /// Flujo completo de Unir Losas: seleccion (minimo 2), validaciones de compatibilidad
    /// con el ElementId que falla, union en la geometria pura, una losa por region, la de
    /// mayor area como maestra de parametros, barras, uniones y borrado de las originales.
    /// </summary>
    public static class MergeOperation
    {
        /// <summary>Diferencia admisible de desfase y de plano entre losas (1 mm).</summary>
        private const double OffsetTolerance = LosasSettings.FeetPerMillimeter;

        public static OperationSummary Run(UIDocument uidoc, LosasSettings settings)
        {
            Document doc = uidoc.Document;
            var summary = new OperationSummary { Title = "Unir losas" };

            // 1. seleccion
            var floors = new List<Floor>();
            try
            {
                IList<Reference> refs = uidoc.Selection.PickObjects(Autodesk.Revit.UI.Selection.ObjectType.Element, new FloorSelectionFilter(),
                    "Selecciona las losas a unir (minimo 2) y pulsa Finalizar");
                foreach (Reference r in refs)
                    if (doc.GetElement(r) is Floor f && floors.All(x => x.Id != f.Id)) floors.Add(f);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return null; }
            if (floors.Count == 0) return null;
            if (floors.Count < 2) throw new AbortException("Hacen falta al menos dos losas; se selecciono solo la " + floors[0].Id.Value + ".");

            // 2. validaciones
            foreach (Floor f in floors) FloorInspector.Validate(f);
            Floor first = floors[0];
            double firstOffset = FloorInspector.LevelOffset(first);
            bool firstStructural = FloorInspector.IsStructural(first);
            foreach (Floor f in floors.Skip(1))
            {
                if (f.FloorType.Id != first.FloorType.Id)
                    throw new AbortException("La losa " + f.Id.Value + " es del tipo \"" + f.FloorType.Name + "\" y la " + first.Id.Value +
                                             " del tipo \"" + first.FloorType.Name + "\": deben ser del mismo tipo.");
                if (f.LevelId != first.LevelId)
                    throw new AbortException("La losa " + f.Id.Value + " esta en un nivel distinto al de la losa " + first.Id.Value + ".");
                if (Math.Abs(FloorInspector.LevelOffset(f) - firstOffset) > OffsetTolerance)
                    throw new AbortException("La losa " + f.Id.Value + " tiene un desfase respecto al nivel distinto al de la losa " + first.Id.Value +
                                             " (" + Mm(FloorInspector.LevelOffset(f)) + " frente a " + Mm(firstOffset) + " mm).");
                if (FloorInspector.IsStructural(f) != firstStructural)
                    throw new AbortException("La losa " + f.Id.Value + " y la " + first.Id.Value + " no coinciden en el parametro Estructural.");
            }

            var profiles = new List<FloorProfile>();
            foreach (Floor f in floors)
            {
                FloorProfile p = FloorProfileReader.Read(f);
                summary.Warnings.AddRange(p.Warnings);
                profiles.Add(p);
            }
            foreach (FloorProfile p in profiles.Skip(1))
                if (Math.Abs(p.Plane.Z - profiles[0].Plane.Z) > OffsetTolerance)
                    throw new AbortException("La losa " + p.Floor.Id.Value + " no es coplanar con la losa " + first.Id.Value +
                                             " (Z del boceto " + Mm(p.Plane.Z) + " frente a " + Mm(profiles[0].Plane.Z) + " mm).");

            // 3. acero y dependientes
            var reports = floors.Select(DependentsInspector.Inspect).ToList();
            var rebars = reports.SelectMany(r => r.Rebars).ToList();
            summary.RebarsTotal = rebars.Count;
            if (rebars.Count > 0 && !settings.KeepRebar)
            {
                DependentReport worst = reports.First(r => r.Rebars.Count > 0);
                throw new AbortException("La losa " + worst.Floor.Id.Value + " tiene " + worst.Rebars.Count + " barras (" + rebars.Count +
                                         " en total). Divide antes de armar o elimina el acero (o activa \"Conservar barras\").");
            }
            if (settings.DeleteOriginal)
                foreach (DependentReport r in reports) summary.Warnings.AddRange(r.LossWarnings());

            // 4. union en la geometria pura
            double shortCurve = uidoc.Application.Application.ShortCurveTolerance;
            MergeResult merge = SlabMerger.Merge(profiles.Select(p => p.Polygon).ToList(), settings.ToMergeOptions(shortCurve));
            foreach (GeometryWarning w in merge.Warnings)
                if (w.Kind != WarningKind.ShortEdgeCollapsed) summary.Warnings.Add(w.Message);
            if (merge.Regions.Count == 0) throw new AbortException("La union no produjo ninguna region valida.");
            if (merge.Regions.Count > 1)
                summary.Warnings.Add("Las losas no se tocan: se crean " + merge.Regions.Count + " losas, una por region desconectada.");

            // 5. losa maestra: la de mayor area
            Floor master = floors.OrderByDescending(f => profiles[floors.IndexOf(f)].Polygon.Area).First();
            HashSet<ElementId> joined = settings.KeepJoins ? JoinManager.CollectJoined(doc, floors) : new HashSet<ElementId>();
            var revitWarnings = new List<string>();

            using (var group = new TransactionGroup(doc, "ARBA: Unir " + floors.Count + " losas"))
            {
                group.Start();
                var created = new List<(Floor Floor, Polygon2 Region)>();
                try
                {
                    using (var t = new Transaction(doc, "Crear losa unida"))
                    {
                        OverlapWarningPreprocessor.Apply(t, !settings.DeleteOriginal, revitWarnings);
                        t.Start();
                        int index = 0;
                        foreach (Polygon2 region in merge.Regions)
                        {
                            index++;
                            Floor f;
                            try { f = FloorFactory.Create(doc, region, profiles, master, summary.Warnings); }
                            catch (Exception ex) when (ex is Autodesk.Revit.Exceptions.ApplicationException || ex is InvalidOperationException)
                            {
                                throw new AbortException("No se pudo crear la losa unida (region " + index + ", area " +
                                                         SplitOperation.FormatArea(region.Area) + "): " + ex.Message);
                            }
                            if (settings.CopyParameters) ParameterCopier.Copy(master, f, index, summary.Warnings);
                            created.Add((f, region));
                        }
                        doc.Regenerate();
                        if (settings.KeepRebar && rebars.Count > 0)
                            summary.RebarsRehosted = RebarRehoster.Rehost(doc, rebars, created, profiles[0].Plane, summary.Warnings);
                        t.Commit();
                    }

                    if (settings.DeleteOriginal)
                    {
                        using (var t = new Transaction(doc, "Eliminar losas originales"))
                        {
                            OverlapWarningPreprocessor.Apply(t, false, revitWarnings);
                            t.Start();
                            doc.Delete(floors.Select(f => f.Id).ToList());
                            summary.Deleted.AddRange(floors.Select(f => f.Id));
                            t.Commit();
                        }
                    }
                    else
                    {
                        summary.Warnings.Add("Se conservan las losas originales: la losa unida se superpone a ellas.");
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

            summary.Warnings.AddRange(revitWarnings.Distinct());
            return summary;
        }

        private static string Mm(double feet) => (feet * 304.8).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    }
}
