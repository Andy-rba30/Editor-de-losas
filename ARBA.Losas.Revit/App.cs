using System;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ARBA.Losas.Revit.Commands;
using Autodesk.Revit.UI;

namespace ARBA.Losas.Revit
{
    /// <summary>
    /// Al arrancar Revit anade a la pestana "ARBA" (se reutiliza si otro add-in de la suite
    /// ya la creo) el panel "Losas" con los botones Dividir Losa, Unir Losas y
    /// Configuracion. Los iconos se dibujan en codigo, como en el resto de ARBA, para no
    /// depender de archivos de imagen.
    /// </summary>
    public class App : IExternalApplication
    {
        public const string TabName = "ARBA";
        public const string PanelName = "Losas";

        public Result OnStartup(UIControlledApplication app)
        {
            try
            {
                // la pestana puede existir ya si otro add-in de ARBA la creo antes
                try { app.CreateRibbonTab(TabName); }
                catch (Autodesk.Revit.Exceptions.ArgumentException) { /* ya existe */ }

                RibbonPanel panel = null;
                foreach (RibbonPanel p in app.GetRibbonPanels(TabName))
                    if (p.Name == PanelName) { panel = p; break; }
                if (panel == null) panel = app.CreateRibbonPanel(TabName, PanelName);

                string assembly = Assembly.GetExecutingAssembly().Location;

                var split = new PushButtonData("ArbaLosasDividir", "Dividir\nlosa", assembly, typeof(SplitFloorCommand).FullName)
                {
                    ToolTip = "Divide una losa en varias losas independientes segun lineas de corte.",
                    LongDescription = "Selecciona la losa, dibuja las lineas de corte (o elige lineas de modelo, de detalle o " +
                                      "rejillas existentes) y se crean las losas resultantes con los mismos parametros. " +
                                      "Los arcos del boceto se conservan y los huecos se reparten o se parten."
                };
                var merge = new PushButtonData("ArbaLosasUnir", "Unir\nlosas", assembly, typeof(MergeFloorsCommand).FullName)
                {
                    ToolTip = "Une dos o mas losas compatibles en una sola.",
                    LongDescription = "Las losas deben tener el mismo tipo, nivel, desfase y flag estructural, sin forma editada " +
                                      "ni pendiente. Los microgaps menores que la tolerancia de union se cierran."
                };
                var settings = new PushButtonData("ArbaLosasConfiguracion", "Configuracion", assembly, typeof(SettingsCommand).FullName)
                {
                    ToolTip = "Opciones de division y union de losas.",
                    LongDescription = "Modo de lineas de corte, extension, borrado de la original, copia de parametros, uniones, " +
                                      "barras y tolerancias. Se guardan en %AppData%\\ARBA\\Losas\\settings.json."
                };
                SetIcons(split, Icons.Split);
                SetIcons(merge, Icons.Merge);
                SetIcons(settings, Icons.Settings);
                panel.AddItem(split);
                panel.AddItem(merge);
                panel.AddItem(settings);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("ARBA Losas", "No se pudo crear el panel Losas de la pestana ARBA: " + ex.Message +
                                "\nLos comandos siguen disponibles en Complementos > Herramientas externas.");
                return Result.Succeeded;
            }
        }

        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;

        private static void SetIcons(PushButtonData data, Func<int, BitmapSource> painter)
        {
            try
            {
                data.LargeImage = painter(32);
                data.Image = painter(16);
            }
            catch (Exception)
            {
                // sin icono el boton sigue funcionando; el error no aporta nada al usuario
            }
        }
    }

    /// <summary>Iconos de los botones dibujados con WPF a la escala pedida (16 o 32 px).</summary>
    internal static class Icons
    {
        private static readonly Color Concrete = Color.FromRgb(0xD9, 0xD9, 0xD9);
        private static readonly Color Edge = Color.FromRgb(0x55, 0x55, 0x55);
        private static readonly Color Accent = Color.FromRgb(0xC0, 0x39, 0x2B);
        private static readonly Color Teal = Color.FromRgb(0x1F, 0x7A, 0x7A);

        /// <summary>Losa en planta partida por una linea de corte roja.</summary>
        public static BitmapSource Split(int size) => Render(size, (dc, s) =>
        {
            var fill = new SolidColorBrush(Concrete);
            var edge = new Pen(new SolidColorBrush(Edge), 1.4 * s);
            var cut = new Pen(new SolidColorBrush(Accent), 2.2 * s) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawRectangle(fill, edge, new Rect(3 * s, 5 * s, 26 * s, 22 * s));
            dc.DrawLine(cut, new Point(19 * s, 2 * s), new Point(13 * s, 30 * s));
        });

        /// <summary>Dos losas que se juntan, con una flecha doble entre ellas.</summary>
        public static BitmapSource Merge(int size) => Render(size, (dc, s) =>
        {
            var fill = new SolidColorBrush(Concrete);
            var edge = new Pen(new SolidColorBrush(Edge), 1.4 * s);
            var arrow = new Pen(new SolidColorBrush(Teal), 2.2 * s) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            dc.DrawRectangle(fill, edge, new Rect(2 * s, 8 * s, 11 * s, 16 * s));
            dc.DrawRectangle(fill, edge, new Rect(19 * s, 8 * s, 11 * s, 16 * s));
            dc.DrawLine(arrow, new Point(9 * s, 16 * s), new Point(23 * s, 16 * s));
            dc.DrawLine(arrow, new Point(12 * s, 12.5 * s), new Point(9 * s, 16 * s));
            dc.DrawLine(arrow, new Point(12 * s, 19.5 * s), new Point(9 * s, 16 * s));
            dc.DrawLine(arrow, new Point(20 * s, 12.5 * s), new Point(23 * s, 16 * s));
            dc.DrawLine(arrow, new Point(20 * s, 19.5 * s), new Point(23 * s, 16 * s));
        });

        /// <summary>Losa pequena con un engranaje esquematico.</summary>
        public static BitmapSource Settings(int size) => Render(size, (dc, s) =>
        {
            var fill = new SolidColorBrush(Concrete);
            var edge = new Pen(new SolidColorBrush(Edge), 1.4 * s);
            var gear = new Pen(new SolidColorBrush(Edge), 2.4 * s) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawRectangle(fill, edge, new Rect(2 * s, 18 * s, 16 * s, 11 * s));
            var c = new Point(21 * s, 11 * s);
            dc.DrawEllipse(null, gear, c, 5 * s, 5 * s);
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                dc.DrawLine(gear, new Point(c.X + Math.Cos(a) * 6 * s, c.Y + Math.Sin(a) * 6 * s),
                                  new Point(c.X + Math.Cos(a) * 8.5 * s, c.Y + Math.Sin(a) * 8.5 * s));
            }
        });

        private static BitmapSource Render(int size, Action<DrawingContext, double> paint)
        {
            double s = size / 32.0;
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen()) paint(dc, s);
            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);
            bmp.Freeze();
            return bmp;
        }
    }
}
