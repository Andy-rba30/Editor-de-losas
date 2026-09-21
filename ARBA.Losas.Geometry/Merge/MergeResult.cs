using System.Collections.Generic;

namespace ARBA.Losas.Geometry
{
    public sealed class MergeResult
    {
        /// <summary>Regiones resultantes. Mas de una significa losas desconectadas.</summary>
        public IReadOnlyList<Polygon2> Regions { get; }
        public IReadOnlyList<GeometryWarning> Warnings { get; }

        public MergeResult(IReadOnlyList<Polygon2> regions, IReadOnlyList<GeometryWarning> warnings)
        {
            Regions = regions;
            Warnings = warnings;
        }
    }
}
