using ARBA.Losas.Revit.Operations;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ARBA.Losas.Revit.Commands
{
    /// <summary>Dividir Losa: una losa existente en N losas independientes segun lineas de corte.</summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SplitFloorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements) =>
            CommandBase.Run(commandData, ref message, "Dividir losa", "Dividir", SplitOperation.Run);
    }
}
