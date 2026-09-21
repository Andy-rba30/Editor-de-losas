using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace ARBA.Losas.Revit.Selection
{
    /// <summary>Solo losas (Floor, categoria OST_Floors). Cubiertas, partes y elementos de vinculos quedan fuera.</summary>
    public sealed class FloorSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (!(elem is Floor)) return false;
            Category cat = elem.Category;
            return cat != null && cat.BuiltInCategory == BuiltInCategory.OST_Floors;
        }

        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
