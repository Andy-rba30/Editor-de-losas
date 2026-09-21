using ARBA.Losas.Geometry;

namespace ARBA.Losas.Revit.Settings
{
    public enum CutLineMode
    {
        /// <summary>Dibujar las lineas al vuelo con PickPoint.</summary>
        Draw,
        /// <summary>Seleccionar lineas de modelo, de detalle o rejillas existentes.</summary>
        Select
    }

    /// <summary>
    /// Opciones del add-in tal como se guardan en %AppData%\ARBA\Losas\settings.json.
    /// Las distancias se editan en milimetros y las areas en metros cuadrados; la
    /// conversion a unidades internas de Revit (pies) esta en los metodos ToSplitOptions y
    /// ToMergeOptions.
    /// </summary>
    public sealed class LosasSettings
    {
        public const double FeetPerMillimeter = 1.0 / 304.8;
        public const double SquareFeetPerSquareMeter = 10.7639104167097;

        public CutLineMode CutLineMode { get; set; } = CutLineMode.Draw;
        public bool ExtendCutLines { get; set; } = true;
        public bool DeleteOriginal { get; set; } = true;
        public bool CopyParameters { get; set; } = true;
        public bool KeepJoins { get; set; } = true;
        /// <summary>Re-alojar las barras en las losas nuevas en vez de abortar.</summary>
        public bool KeepRebar { get; set; } = false;
        public double TessellationToleranceMm { get; set; } = 1.0;
        public double JoinToleranceMm { get; set; } = 1.0;
        public double MinAreaM2 { get; set; } = 0.01;
        /// <summary>Factor de escala a enteros de Clipper2 sobre unidades internas.</summary>
        public double ClipperScale { get; set; } = 1e6;

        /// <summary><paramref name="shortCurveTolerance"/> viene de Application.ShortCurveTolerance.</summary>
        public SplitOptions ToSplitOptions(double shortCurveTolerance) => new SplitOptions
        {
            TessellationTolerance = TessellationToleranceMm * FeetPerMillimeter,
            ShortCurveTolerance = shortCurveTolerance,
            MinArea = MinAreaM2 * SquareFeetPerSquareMeter,
            Scale = ClipperScale,
            ExtendCutLines = ExtendCutLines
        };

        public MergeOptions ToMergeOptions(double shortCurveTolerance) => new MergeOptions
        {
            TessellationTolerance = TessellationToleranceMm * FeetPerMillimeter,
            JoinTolerance = JoinToleranceMm * FeetPerMillimeter,
            ShortCurveTolerance = shortCurveTolerance,
            MinArea = MinAreaM2 * SquareFeetPerSquareMeter,
            Scale = ClipperScale
        };

        /// <summary>Copia con valores corregidos: sin negativos ni ceros donde no tienen sentido.</summary>
        public LosasSettings Sanitized()
        {
            return new LosasSettings
            {
                CutLineMode = CutLineMode,
                ExtendCutLines = ExtendCutLines,
                DeleteOriginal = DeleteOriginal,
                CopyParameters = CopyParameters,
                KeepJoins = KeepJoins,
                KeepRebar = KeepRebar,
                TessellationToleranceMm = TessellationToleranceMm > 0 ? TessellationToleranceMm : 1.0,
                JoinToleranceMm = JoinToleranceMm >= 0 ? JoinToleranceMm : 1.0,
                MinAreaM2 = MinAreaM2 >= 0 ? MinAreaM2 : 0.01,
                ClipperScale = ClipperScale > 0 ? ClipperScale : 1e6
            };
        }
    }
}
