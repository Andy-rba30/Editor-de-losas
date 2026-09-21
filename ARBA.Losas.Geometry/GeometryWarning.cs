namespace ARBA.Losas.Geometry
{
    public enum WarningKind
    {
        /// <summary>La linea de corte no dividio ninguna region (no la cruza, es tangente o colineal con un borde).</summary>
        CutDidNotDivide,
        /// <summary>Modo sin extender: la linea no cruza la region de lado a lado.</summary>
        CutDoesNotCross,
        /// <summary>Region descartada por area menor al umbral.</summary>
        SliverDiscarded,
        /// <summary>Vertice de salida sin correspondencia con la entrada; se trato como vertice recto.</summary>
        VertexUnmatched,
        /// <summary>Se fusionaron dos vertices mas cercanos que la tolerancia de curva corta.</summary>
        ShortEdgeCollapsed,
        /// <summary>La union produjo varias regiones desconectadas.</summary>
        DisconnectedRegions,
        /// <summary>Los microgaps se cerraron por inflado y desinflado; algun borde curvo pudo quedar como polilinea.</summary>
        GapsClosedByInflate,
        /// <summary>Una curva de entrada no era linea ni arco y se teselo como polilinea.</summary>
        CurveTessellated
    }

    /// <summary>Advertencia acumulada durante una operacion; nunca interrumpe el resultado.</summary>
    public sealed class GeometryWarning
    {
        public WarningKind Kind { get; }
        public string Message { get; }
        /// <summary>Indice de la linea de corte o de la losa de entrada implicada, si aplica.</summary>
        public int? Index { get; }

        public GeometryWarning(WarningKind kind, string message, int? index = null)
        {
            Kind = kind;
            Message = message;
            Index = index;
        }

        public override string ToString() => Kind + ": " + Message;
    }
}
