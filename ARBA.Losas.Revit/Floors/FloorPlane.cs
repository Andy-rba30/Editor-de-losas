using ARBA.Losas.Geometry;
using Autodesk.Revit.DB;

namespace ARBA.Losas.Revit.Floors
{
    /// <summary>
    /// Transformacion 2D-3D del plano de la losa. Las losas de la version 1 son horizontales,
    /// asi que el plano es Z = constante y el paso es dejar caer o recuperar la Z.
    /// Floor.Create ignora la Z de los loops (coloca la losa en nivel + desfase), pero se
    /// devuelve la misma Z del boceto para que las curvas generadas coincidan con las
    /// originales y con las lineas de corte proyectadas.
    /// </summary>
    public sealed class FloorPlane
    {
        public double Z { get; }

        public FloorPlane(double z) { Z = z; }

        public Vec2 To2D(XYZ p) => new Vec2(p.X, p.Y);

        public XYZ To3D(Vec2 p) => new XYZ(p.X, p.Y, Z);
    }
}
