using System;
using ARBA.Losas.Revit.Operations;
using ARBA.Losas.Revit.Settings;
using ARBA.Losas.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ARBA.Losas.Revit.Commands
{
    /// <summary>Carga de opciones, ventana, ejecucion y resumen comunes a Dividir y Unir.</summary>
    internal static class CommandBase
    {
        public static Result Run(ExternalCommandData commandData, ref string message, string title, string acceptText,
                                 Func<UIDocument, LosasSettings, OperationSummary> operation)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) { message = "No hay ningun documento abierto."; return Result.Cancelled; }
            if (uidoc.Document.IsFamilyDocument) { message = "El comando solo funciona en proyectos, no en familias."; return Result.Cancelled; }

            LosasSettings settings = SettingsStore.Load(out string problem);
            var window = new OptionsWindow(settings, acceptText, uiapp.MainWindowHandle);
            if (window.ShowDialog() != true) return Result.Cancelled;
            settings = window.Result;

            try
            {
                OperationSummary summary = operation(uidoc, settings);
                if (summary == null) return Result.Cancelled;
                if (problem != null) summary.Warnings.Insert(0, problem);
                SummaryDialog.Show(summary);
                return Result.Succeeded;
            }
            catch (AbortException ex)
            {
                SummaryDialog.ShowAbort(title, ex.Message);
                return Result.Cancelled;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException ex)
            {
                message = "Error de Revit en " + title + ": " + ex.Message;
                return Result.Failed;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is NullReferenceException)
            {
                // fallo de la geometria o de la traduccion: se reporta con detalle; el TransactionGroup ya se deshizo
                message = "Error interno en " + title + ": " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace;
                return Result.Failed;
            }
        }
    }
}
