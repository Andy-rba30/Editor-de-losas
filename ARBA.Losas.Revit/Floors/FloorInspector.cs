using System;
using ARBA.Losas.Revit.Operations;
using Autodesk.Revit.DB;

namespace ARBA.Losas.Revit.Floors
{
    /// <summary>
    /// Condiciones que dejan una losa fuera del alcance de la version 1. Cada comprobacion
    /// lanza AbortException con la condicion exacta y el ElementId.
    /// </summary>
    public static class FloorInspector
    {
        public static void Validate(Floor floor)
        {
            long id = floor.Id.Value;
            Document doc = floor.Document;

            if (doc.IsFamilyDocument)
                throw new AbortException("La losa " + id + " esta dentro de una familia: el comando solo funciona en proyectos.");

            // espesor variable: alguna capa del tipo es de espesor variable
            CompoundStructure cs = floor.FloorType?.GetCompoundStructure();
            if (cs != null && cs.VariableLayerIndex >= 0)
                throw new AbortException("La losa " + id + " tiene espesor variable (capa " + cs.VariableLayerIndex + " del tipo \"" +
                                         floor.FloorType.Name + "\"). Solo se admiten losas de espesor constante.");

            // edicion de forma
            SlabShapeEditor editor = floor.GetSlabShapeEditor();
            if (editor != null && editor.IsEnabled)
            {
                string detail = "";
                try
                {
                    int modified = 0;
                    SlabShapeVertexArray vertices = editor.SlabShapeVertices;
                    for (int i = 0; i < vertices.Size; i++)
                        if (vertices.get_Item(i).VertexType != SlabShapeVertexType.Corner) modified++;
                    int creases = 0;
                    SlabShapeCreaseArray creaseArray = editor.SlabShapeCreases;
                    for (int i = 0; i < creaseArray.Size; i++)
                        if (creaseArray.get_Item(i).CreaseType == SlabShapeCreaseType.UserDrawn) creases++;
                    detail = " (" + modified + " puntos y " + creases + " aristas anadidos)";
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException)
                {
                    detail = "";
                }
                throw new AbortException("La losa " + id + " tiene la edicion de forma habilitada" + detail +
                                         ". Restablece la forma antes de dividir o unir.");
            }

            // inclinacion: caras superiores planas y con normal paralela a Z (no se confia solo en el parametro de pendiente)
            try
            {
                foreach (Reference r in HostObjectUtils.GetTopFaces(floor))
                {
                    if (!(floor.GetGeometryObjectFromReference(r) is PlanarFace face))
                        throw new AbortException("La losa " + id + " tiene una cara superior no plana: losa inclinada o con forma editada.");
                    if (Math.Abs(Math.Abs(face.FaceNormal.Normalize().Z) - 1) > 1e-9)
                        throw new AbortException("La losa " + id + " esta inclinada (normal de la cara superior " + Fmt(face.FaceNormal) + ").");
                }
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException ex)
            {
                throw new AbortException("No se pudieron analizar las caras superiores de la losa " + id + ": " + ex.Message);
            }

            Parameter slope = floor.get_Parameter(BuiltInParameter.ROOF_SLOPE);
            if (slope != null && slope.StorageType == StorageType.Double && Math.Abs(slope.AsDouble()) > 1e-9)
                throw new AbortException("La losa " + id + " tiene pendiente (" + slope.AsValueString() + ").");
        }

        private static string Fmt(XYZ v) =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}, {2:0.###})", v.X, v.Y, v.Z);

        /// <summary>Desfase respecto al nivel (FLOOR_HEIGHTABOVELEVEL_PARAM) en unidades internas.</summary>
        public static double LevelOffset(Floor floor)
        {
            Parameter p = floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
            return p != null && p.StorageType == StorageType.Double ? p.AsDouble() : 0;
        }

        public static bool IsStructural(Floor floor)
        {
            Parameter p = floor.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL);
            return p != null && p.StorageType == StorageType.Integer && p.AsInteger() != 0;
        }
    }
}
