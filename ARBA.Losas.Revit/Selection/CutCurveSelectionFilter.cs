using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace ARBA.Losas.Revit.Selection
{
    /// <summary>Lineas de corte existentes: lineas de modelo, lineas de detalle y rejillas.</summary>
    public sealed class CutCurveSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is ModelCurve || elem is DetailCurve || elem is Grid;

        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
