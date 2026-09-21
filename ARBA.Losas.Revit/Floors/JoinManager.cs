using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace ARBA.Losas.Revit.Floors
{
    /// <summary>Guarda las uniones de geometria de las originales y las reproduce en las losas nuevas, par a par.</summary>
    public static class JoinManager
    {
        /// <summary>Ids de los elementos unidos a las losas dadas, excluyendo las propias losas.</summary>
        public static HashSet<ElementId> CollectJoined(Document doc, IEnumerable<Floor> floors)
        {
            var exclude = new HashSet<ElementId>();
            foreach (Floor f in floors) exclude.Add(f.Id);
            var result = new HashSet<ElementId>();
            foreach (Floor f in floors)
                foreach (ElementId id in JoinGeometryUtils.GetJoinedElements(doc, f))
                    if (!exclude.Contains(id)) result.Add(id);
            return result;
        }

        /// <summary>Une cada losa nueva con cada elemento; cada par en su propio try/catch y el motivo del fallo va a las advertencias.</summary>
        public static int Rejoin(Document doc, IEnumerable<Floor> newFloors, IEnumerable<ElementId> joinedIds, List<string> warnings)
        {
            int done = 0;
            foreach (Floor floor in newFloors)
                foreach (ElementId id in joinedIds)
                {
                    Element other = doc.GetElement(id);
                    if (other == null) continue;
                    try
                    {
                        if (JoinGeometryUtils.AreElementsJoined(doc, floor, other)) { done++; continue; }
                        JoinGeometryUtils.JoinGeometry(doc, floor, other);
                        done++;
                    }
                    catch (Autodesk.Revit.Exceptions.ApplicationException ex)
                    {
                        warnings.Add("No se pudo unir la losa " + floor.Id.Value + " con el elemento " + id.Value + ": " + ex.Message);
                    }
                }
            return done;
        }
    }
}
