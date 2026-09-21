using System.Collections.Generic;

namespace ARBA.Losas.Geometry.Internal
{
    /// <summary>Curvas originales de entrada, numeradas, con la procedencia de cada una. El id es el indice.</summary>
    internal sealed class CurveTable
    {
        private readonly List<ICurve2> _curves = new List<ICurve2>();
        private readonly List<CurveOrigin> _origins = new List<CurveOrigin>();

        public int Count => _curves.Count;
        public ICurve2 this[int id] => _curves[id];
        public CurveOrigin OriginOf(int id) => _origins[id];

        /// <summary>Registra una curva; <paramref name="origin"/> dice de que curva de entrada y que tramo procede.</summary>
        public int Add(ICurve2 curve, CurveOrigin origin)
        {
            _curves.Add(curve);
            _origins.Add(origin);
            return _curves.Count - 1;
        }
    }
}
