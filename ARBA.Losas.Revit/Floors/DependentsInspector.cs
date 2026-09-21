using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace ARBA.Losas.Revit.Floors
{
    /// <summary>Elementos que dependen de una losa y que se perderian (o hay que re-alojar) al borrarla.</summary>
    public sealed class DependentReport
    {
        public Floor Floor { get; }
        public List<Rebar> Rebars { get; } = new List<Rebar>();
        /// <summary>Areas y caminos de refuerzo, mallas y contenedores: no se re-alojan, se avisa.</summary>
        public List<string> OtherReinforcement { get; } = new List<string>();
        public List<ElementId> Openings { get; } = new List<ElementId>();
        public List<ElementId> SlabEdges { get; } = new List<ElementId>();
        public List<ElementId> HostedInstances { get; } = new List<ElementId>();
        /// <summary>Otros dependientes que no son del boceto, agrupados por categoria.</summary>
        public Dictionary<string, int> Others { get; } = new Dictionary<string, int>();

        public DependentReport(Floor floor) { Floor = floor; }

        /// <summary>Advertencias de lo que se perdera al borrar la original.</summary>
        public IEnumerable<string> LossWarnings()
        {
            long id = Floor.Id.Value;
            foreach (string s in OtherReinforcement) yield return s;
            if (Openings.Count > 0) yield return "La losa " + id + " tiene " + Openings.Count + " hueco(s) por cara (" + Join(Openings) + ") que se perderan al borrar la original.";
            if (SlabEdges.Count > 0) yield return "La losa " + id + " tiene " + SlabEdges.Count + " borde(s) de losa (" + Join(SlabEdges) + ") que se perderan al borrar la original.";
            if (HostedInstances.Count > 0) yield return "La losa " + id + " aloja " + HostedInstances.Count + " familia(s) (" + Join(HostedInstances) + ") que se perderan al borrar la original.";
            foreach (KeyValuePair<string, int> kv in Others)
                yield return "La losa " + id + " tiene " + kv.Value + " elemento(s) dependiente(s) de la categoria \"" + kv.Key + "\" que se perderan al borrar la original.";
        }

        private static string Join(List<ElementId> ids)
        {
            var parts = new List<string>();
            foreach (ElementId i in ids) { if (parts.Count >= 10) { parts.Add("..."); break; } parts.Add(i.Value.ToString()); }
            return string.Join(", ", parts);
        }
    }

    public static class DependentsInspector
    {
        public static DependentReport Inspect(Floor floor)
        {
            var report = new DependentReport(floor);
            Document doc = floor.Document;
            long id = floor.Id.Value;

            RebarHostData host = RebarHostData.GetRebarHostData(floor);
            if (host != null)
            {
                foreach (Rebar r in host.GetRebarsInHost()) report.Rebars.Add(r);
                int areas = host.GetAreaReinforcementsInHost().Count;
                int paths = host.GetPathReinforcementsInHost().Count;
                int sheets = host.GetFabricSheetsInHost().Count;
                int containers = host.GetRebarContainersInHost().Count;
                if (areas > 0) report.OtherReinforcement.Add("La losa " + id + " tiene " + areas + " area(s) de refuerzo que se perderan al borrar la original.");
                if (paths > 0) report.OtherReinforcement.Add("La losa " + id + " tiene " + paths + " camino(s) de refuerzo que se perderan al borrar la original.");
                if (sheets > 0) report.OtherReinforcement.Add("La losa " + id + " tiene " + sheets + " malla(s) que se perderan al borrar la original.");
                if (containers > 0) report.OtherReinforcement.Add("La losa " + id + " tiene " + containers + " contenedor(es) de barras que se perderan al borrar la original.");
            }

            foreach (ElementId depId in floor.GetDependentElements(null))
            {
                if (depId == floor.Id) continue;
                Element e = doc.GetElement(depId);
                switch (e)
                {
                    case null:
                    case Sketch:
                    case SketchPlane:
                    case CurveElement:        // lineas del boceto
                    case Rebar:
                    case AreaReinforcement:
                    case PathReinforcement:
                    case FabricSheet:
                    case FabricArea:
                    case RebarContainer:
                        continue;
                    case Opening:
                        report.Openings.Add(depId);
                        continue;
                    case SlabEdge:
                        report.SlabEdges.Add(depId);
                        continue;
                    case FamilyInstance fi when fi.Host != null && fi.Host.Id == floor.Id:
                        report.HostedInstances.Add(depId);
                        continue;
                }
                // el resto (por ejemplo elementos analiticos o etiquetas) se agrupa por categoria
                if (e.Category == null) continue;
                string cat = e.Category.Name;
                report.Others[cat] = report.Others.TryGetValue(cat, out int n) ? n + 1 : 1;
            }
            return report;
        }
    }
}
