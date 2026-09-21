using ARBA.Losas.Revit.Settings;
using ARBA.Losas.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ARBA.Losas.Revit.Commands
{
    /// <summary>Configuracion: edita y guarda %AppData%\ARBA\Losas\settings.json.</summary>
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class SettingsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            LosasSettings settings = SettingsStore.Load(out string problem);
            var window = new OptionsWindow(settings, "Guardar y cerrar", commandData.Application.MainWindowHandle);
            if (problem != null) TaskDialog.Show("ARBA Losas", problem);
            if (window.ShowDialog() != true) return Result.Cancelled;
            try
            {
                SettingsStore.Save(window.Result);
                return Result.Succeeded;
            }
            catch (System.Exception ex) when (ex is System.IO.IOException || ex is System.UnauthorizedAccessException)
            {
                message = "No se pudo guardar " + SettingsStore.FilePath + ": " + ex.Message;
                return Result.Failed;
            }
        }
    }
}
