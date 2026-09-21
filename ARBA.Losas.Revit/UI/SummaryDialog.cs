using System.Collections.Generic;
using System.Linq;
using ARBA.Losas.Revit.Operations;
using Autodesk.Revit.UI;

namespace ARBA.Losas.Revit.UI
{
    /// <summary>Resumen final del comando: losas creadas y borradas, barras re-alojadas y advertencias. Fuera del bucle de picking.</summary>
    public static class SummaryDialog
    {
        public static void Show(OperationSummary s)
        {
            var td = new TaskDialog("ARBA Losas: " + s.Title) { TitleAutoPrefix = false, CommonButtons = TaskDialogCommonButtons.Close };
            var lines = new List<string>
            {
                "Losas creadas: " + s.Created.Count + (s.Created.Count > 0 ? " (" + string.Join(", ", s.Created.Select(i => i.Value)) + ")" : ""),
                "Losas borradas: " + s.Deleted.Count + (s.Deleted.Count > 0 ? " (" + string.Join(", ", s.Deleted.Select(i => i.Value)) + ")" : "")
            };
            if (s.RebarsTotal > 0) lines.Add("Barras re-alojadas: " + s.RebarsRehosted + " de " + s.RebarsTotal);
            if (s.JoinsRestored > 0) lines.Add("Uniones restablecidas: " + s.JoinsRestored);
            lines.Add("Advertencias: " + s.Warnings.Count);
            td.MainInstruction = s.Title + " completado";
            td.MainContent = string.Join("\n", lines);
            if (s.Warnings.Count > 0)
            {
                td.MainIcon = TaskDialogIcon.TaskDialogIconWarning;
                td.ExpandedContent = string.Join("\n", s.Warnings.Select(w => "• " + w));
            }
            td.Show();
        }

        public static void ShowAbort(string title, string message)
        {
            var td = new TaskDialog("ARBA Losas: " + title)
            {
                TitleAutoPrefix = false,
                MainIcon = TaskDialogIcon.TaskDialogIconError,
                MainInstruction = "No se realizo ningun cambio",
                MainContent = message,
                CommonButtons = TaskDialogCommonButtons.Close
            };
            td.Show();
        }
    }
}
