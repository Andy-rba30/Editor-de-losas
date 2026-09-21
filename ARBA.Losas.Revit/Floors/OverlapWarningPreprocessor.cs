using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace ARBA.Losas.Revit.Floors
{
    /// <summary>
    /// Preprocesador de fallos de la transaccion: silencia solo el aviso de losas
    /// solapadas, y solo cuando se conserva la original (las nuevas se superponen a ella
    /// por definicion). Los demas avisos se dejan pasar y se anotan para el resumen.
    /// </summary>
    public sealed class OverlapWarningPreprocessor : IFailuresPreprocessor
    {
        private readonly bool _swallowOverlap;
        private readonly List<string> _revitWarnings;

        public OverlapWarningPreprocessor(bool swallowOverlap, List<string> revitWarnings)
        {
            _swallowOverlap = swallowOverlap;
            _revitWarnings = revitWarnings;
        }

        public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
        {
            foreach (FailureMessageAccessor f in accessor.GetFailureMessages())
            {
                if (f.GetSeverity() != FailureSeverity.Warning) continue;
                if (_swallowOverlap && f.GetFailureDefinitionId() == BuiltInFailures.OverlapFailures.FloorsOverlap)
                {
                    accessor.DeleteWarning(f);
                    continue;
                }
                _revitWarnings.Add("Revit: " + f.GetDescriptionText());
            }
            return FailureProcessingResult.Continue;
        }

        public static FailureHandlingOptions Apply(Transaction t, bool swallowOverlap, List<string> revitWarnings)
        {
            FailureHandlingOptions options = t.GetFailureHandlingOptions()
                .SetFailuresPreprocessor(new OverlapWarningPreprocessor(swallowOverlap, revitWarnings))
                .SetClearAfterRollback(true);
            t.SetFailureHandlingOptions(options);
            return options;
        }
    }
}
