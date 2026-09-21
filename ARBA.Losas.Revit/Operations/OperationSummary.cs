using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace ARBA.Losas.Revit.Operations
{
    /// <summary>Resultado de un comando para el resumen final.</summary>
    public sealed class OperationSummary
    {
        public string Title { get; set; }
        public List<ElementId> Created { get; } = new List<ElementId>();
        public List<ElementId> Deleted { get; } = new List<ElementId>();
        public int RebarsRehosted { get; set; }
        public int RebarsTotal { get; set; }
        public int JoinsRestored { get; set; }
        public List<string> Warnings { get; } = new List<string>();
        /// <summary>True si el modelo no cambio (cancelado o abortado).</summary>
        public bool NoChanges => Created.Count == 0 && Deleted.Count == 0;
    }
}
