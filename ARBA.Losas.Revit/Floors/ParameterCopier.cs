using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace ARBA.Losas.Revit.Floors
{
    /// <summary>
    /// Copia a la losa nueva los parametros editables de la original: desfase de nivel,
    /// estructural, comentarios, nota clave, compartidos y de proyecto, fases y workset.
    /// La marca se numera (Marca-1, Marca-2, ...) porque Revit avisa de marcas duplicadas.
    /// </summary>
    public static class ParameterCopier
    {
        /// <summary>
        /// Parametros que no se copian aunque no sean de solo lectura. Los calculados (area,
        /// perimetro, volumen, elevaciones) los recalcula Revit; el nivel y el tipo ya se
        /// fijan en Floor.Create; los de identidad, opcion de diseno y bloqueo no tienen sentido.
        /// </summary>
        private static readonly HashSet<BuiltInParameter> Skipped = new HashSet<BuiltInParameter>
        {
            BuiltInParameter.HOST_AREA_COMPUTED,
            BuiltInParameter.HOST_PERIMETER_COMPUTED,
            BuiltInParameter.HOST_VOLUME_COMPUTED,
            BuiltInParameter.STRUCTURAL_ELEVATION_AT_TOP,
            BuiltInParameter.STRUCTURAL_ELEVATION_AT_BOTTOM,
            BuiltInParameter.STRUCTURAL_ELEVATION_AT_TOP_CORE,
            BuiltInParameter.STRUCTURAL_ELEVATION_AT_BOTTOM_CORE,
            BuiltInParameter.STRUCTURAL_ELEVATION_AT_TOP_SURVEY,
            BuiltInParameter.STRUCTURAL_ELEVATION_AT_BOTTOM_SURVEY,
            BuiltInParameter.LEVEL_PARAM,
            BuiltInParameter.SCHEDULE_LEVEL_PARAM,
            BuiltInParameter.ELEM_TYPE_PARAM,
            BuiltInParameter.ELEM_FAMILY_PARAM,
            BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM,
            BuiltInParameter.ELEM_CATEGORY_PARAM,
            BuiltInParameter.ID_PARAM,
            BuiltInParameter.EDITED_BY,
            BuiltInParameter.DESIGN_OPTION_ID,
            BuiltInParameter.ELEMENT_LOCKED_PARAM,
            BuiltInParameter.ELEM_DELETABLE_IN_FAMILY,
            BuiltInParameter.HOST_ID_PARAM,
            BuiltInParameter.ALL_MODEL_MARK,          // se trata aparte (numerada)
            BuiltInParameter.ELEM_PARTITION_PARAM     // workset: solo si el modelo es compartido, se trata aparte
        };

        /// <summary>Copia los parametros. <paramref name="markIndex"/> numera la marca (1, 2, ...).</summary>
        public static void Copy(Floor source, Floor target, int markIndex, List<string> warnings)
        {
            Document doc = source.Document;
            long sid = source.Id.Value, tid = target.Id.Value;

            // explicitos primero: desfase y estructural, que definen la posicion y el comportamiento
            CopyBuiltIn(source, target, BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM, warnings);
            CopyBuiltIn(source, target, BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL, warnings);
            CopyBuiltIn(source, target, BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS, warnings);
            CopyBuiltIn(source, target, BuiltInParameter.KEYNOTE_PARAM, warnings);
            CopyBuiltIn(source, target, BuiltInParameter.PHASE_CREATED, warnings);
            CopyBuiltIn(source, target, BuiltInParameter.PHASE_DEMOLISHED, warnings);

            // marca numerada
            Parameter mark = source.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
            string markValue = mark?.AsString();
            if (!string.IsNullOrEmpty(markValue))
            {
                Parameter tm = target.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                if (tm != null && !tm.IsReadOnly) TrySet(tm, () => tm.Set(markValue + "-" + markIndex), "Marca", tid, warnings);
            }

            // workset solo en modelos compartidos
            if (doc.IsWorkshared)
            {
                Parameter ws = source.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                Parameter tw = target.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                if (ws != null && tw != null && !tw.IsReadOnly)
                    TrySet(tw, () => tw.Set(ws.AsInteger()), "Workset", tid, warnings);
            }

            // el resto: compartidos, de proyecto y cualquier otro incorporado editable
            foreach (Parameter p in source.Parameters)
            {
                if (p == null || p.IsReadOnly || p.StorageType == StorageType.None) continue;
                BuiltInParameter bip = (p.Definition as InternalDefinition)?.BuiltInParameter ?? BuiltInParameter.INVALID;
                if (bip != BuiltInParameter.INVALID && Skipped.Contains(bip)) continue;
                Parameter t = FindOnTarget(target, p, bip);
                if (t == null || t.IsReadOnly || t.StorageType != p.StorageType) continue;
                CopyValue(p, t, tid, warnings);
            }
        }

        private static Parameter FindOnTarget(Element target, Parameter p, BuiltInParameter bip)
        {
            if (p.IsShared) return target.get_Parameter(p.GUID);
            if (bip != BuiltInParameter.INVALID) return target.get_Parameter(bip);
            return target.get_Parameter(p.Definition) ?? target.LookupParameter(p.Definition.Name);
        }

        private static void CopyBuiltIn(Floor source, Floor target, BuiltInParameter bip, List<string> warnings)
        {
            Parameter s = source.get_Parameter(bip);
            Parameter t = target.get_Parameter(bip);
            if (s == null || t == null || t.IsReadOnly || s.StorageType != t.StorageType) return;
            CopyValue(s, t, target.Id.Value, warnings);
        }

        private static void CopyValue(Parameter s, Parameter t, long targetId, List<string> warnings)
        {
            string name = s.Definition.Name;
            switch (s.StorageType)
            {
                case StorageType.Double: TrySet(t, () => t.Set(s.AsDouble()), name, targetId, warnings); break;
                case StorageType.Integer: TrySet(t, () => t.Set(s.AsInteger()), name, targetId, warnings); break;
                case StorageType.String: TrySet(t, () => t.Set(s.AsString() ?? ""), name, targetId, warnings); break;
                case StorageType.ElementId: TrySet(t, () => t.Set(s.AsElementId()), name, targetId, warnings); break;
            }
        }

        private static void TrySet(Parameter t, Func<bool> set, string name, long targetId, List<string> warnings)
        {
            try
            {
                if (!set()) warnings.Add("No se pudo copiar el parametro \"" + name + "\" a la losa " + targetId + ".");
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException ex)
            {
                warnings.Add("No se pudo copiar el parametro \"" + name + "\" a la losa " + targetId + ": " + ex.Message);
            }
        }
    }
}
