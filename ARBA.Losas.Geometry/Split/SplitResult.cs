using System.Collections.Generic;

namespace ARBA.Losas.Geometry
{
    public sealed class SplitResult
    {
        /// <summary>Regiones resultantes, listas para convertirse en losas.</summary>
        public IReadOnlyList<Polygon2> Regions { get; }
        /// <summary>Regiones descartadas por area menor al umbral.</summary>
        public IReadOnlyList<Polygon2> DiscardedSlivers { get; }
        public IReadOnlyList<GeometryWarning> Warnings { get; }

        public SplitResult(IReadOnlyList<Polygon2> regions, IReadOnlyList<Polygon2> discardedSlivers, IReadOnlyList<GeometryWarning> warnings)
        {
            Regions = regions;
            DiscardedSlivers = discardedSlivers;
            Warnings = warnings;
        }
    }
}
