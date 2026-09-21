using ARBA.Losas.Revit.Operations;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ARBA.Losas.Revit.Commands
{
    /// <summary>Unir Losas: dos o mas losas compatibles en una.</summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class MergeFloorsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements) =>
            CommandBase.Run(commandData, ref message, "Unir losas", "Unir", MergeOperation.Run);
    }
}
