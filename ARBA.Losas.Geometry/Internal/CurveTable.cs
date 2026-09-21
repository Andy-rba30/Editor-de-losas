using System.Collections.Generic;

namespace ARBA.Losas.Geometry.Internal
{
    /// <summary>Curvas originales de entrada, numeradas. El id es el indice.</summary>
    internal sealed class CurveTable
    {
        private readonly List<ICurve2> _curves = new List<ICurve2>();

        public int Count => _curves.Count;
        public ICurve2 this[int id] => _curves[id];

        public int Add(ICurve2 curve)
        {
            _curves.Add(curve);
            return _curves.Count - 1;
        }
    }
}
