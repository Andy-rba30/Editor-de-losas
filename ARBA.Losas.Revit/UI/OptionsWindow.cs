using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ARBA.Losas.Revit.Settings;

namespace ARBA.Losas.Revit.UI
{
    /// <summary>
    /// Ventana de opciones construida en codigo (sin XAML, como el resto de ARBA). La misma
    /// ventana sirve para el comando Configuracion (solo guardar) y para Dividir/Unir
    /// (aceptar para ejecutar con estas opciones, con la posibilidad de guardarlas).
    /// </summary>
    public sealed class OptionsWindow : Window
    {
        private readonly RadioButton _draw, _select;
        private readonly CheckBox _extend, _delete, _copy, _joins, _rebar;
        private readonly TextBox _tess, _join, _minArea;
        private readonly TextBlock _error;

        /// <summary>Opciones resultantes si el usuario acepto; null si cancelo.</summary>
        public LosasSettings Result { get; private set; }

        public OptionsWindow(LosasSettings current, string acceptText, IntPtr revitHandle)
        {
            Title = "ARBA Losas: opciones";
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            try { new WindowInteropHelper(this).Owner = revitHandle; }
            catch (Exception) { /* sin owner la ventana sigue funcionando, solo pierde la modalidad sobre Revit */ }

            var root = new StackPanel { Margin = new Thickness(14), MinWidth = 380 };

            root.Children.Add(Header("Lineas de corte (Dividir)"));
            _draw = new RadioButton { Content = "Dibujar al vuelo (puntos encadenados hasta Esc)", Margin = new Thickness(0, 2, 0, 2), IsChecked = current.CutLineMode == CutLineMode.Draw };
            _select = new RadioButton { Content = "Seleccionar lineas de modelo, de detalle o rejillas existentes", Margin = new Thickness(0, 2, 0, 2), IsChecked = current.CutLineMode == CutLineMode.Select };
            root.Children.Add(_draw);
            root.Children.Add(_select);
            _extend = Check("Extender lineas de corte mas alla de la losa", current.ExtendCutLines);
            root.Children.Add(_extend);

            root.Children.Add(Header("Resultado"));
            _delete = Check("Eliminar la(s) losa(s) original(es)", current.DeleteOriginal);
            _copy = Check("Copiar parametros de la original (marca numerada)", current.CopyParameters);
            _joins = Check("Mantener uniones de geometria", current.KeepJoins);
            _rebar = Check("Conservar barras (re-alojarlas en las losas nuevas; si no, abortar cuando haya acero)", current.KeepRebar);
            root.Children.Add(_delete);
            root.Children.Add(_copy);
            root.Children.Add(_joins);
            root.Children.Add(_rebar);

            root.Children.Add(Header("Tolerancias"));
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            _tess = Row(grid, 0, "Teselado de arcos (mm)", current.TessellationToleranceMm);
            _join = Row(grid, 1, "Tolerancia de union / microgaps (mm)", current.JoinToleranceMm);
            _minArea = Row(grid, 2, "Area minima de una losa resultante (m²)", current.MinAreaM2);
            root.Children.Add(grid);
            root.Children.Add(new TextBlock
            {
                Text = "La tolerancia de curva corta se toma de Revit (Application.ShortCurveTolerance).",
                Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap
            });

            _error = new TextBlock { Foreground = System.Windows.Media.Brushes.Firebrick, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
            root.Children.Add(_error);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var save = new Button { Content = "Guardar como valores por defecto", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 8, 0) };
            var ok = new Button { Content = acceptText, Padding = new Thickness(16, 4, 16, 4), Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var cancel = new Button { Content = "Cancelar", Padding = new Thickness(12, 4, 12, 4), IsCancel = true };
            save.Click += (s, e) =>
            {
                LosasSettings v = Read();
                if (v == null) return;
                try { SettingsStore.Save(v); _error.Visibility = Visibility.Collapsed; }
                catch (Exception ex) { ShowError("No se pudo guardar " + SettingsStore.FilePath + ": " + ex.Message); }
            };
            ok.Click += (s, e) =>
            {
                LosasSettings v = Read();
                if (v == null) return;
                Result = v;
                DialogResult = true;
            };
            buttons.Children.Add(save);
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            root.Children.Add(buttons);
            Content = root;
        }

        private static TextBlock Header(string text) => new TextBlock { Text = text, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 8, 0, 4) };

        private static CheckBox Check(string text, bool value) => new CheckBox { Content = text, IsChecked = value, Margin = new Thickness(0, 2, 0, 2) };

        private static TextBox Row(Grid grid, int row, string label, double value)
        {
            grid.RowDefinitions.Add(new RowDefinition());
            var l = new TextBlock { Text = label, Margin = new Thickness(0, 3, 10, 3), VerticalAlignment = VerticalAlignment.Center };
            var t = new TextBox { Text = value.ToString("0.###", CultureInfo.InvariantCulture), Margin = new Thickness(0, 2, 0, 2) };
            Grid.SetRow(l, row); Grid.SetColumn(l, 0);
            Grid.SetRow(t, row); Grid.SetColumn(t, 1);
            grid.Children.Add(l);
            grid.Children.Add(t);
            return t;
        }

        private LosasSettings Read()
        {
            if (!TryParse(_tess.Text, out double tess) || tess <= 0) { ShowError("El teselado de arcos debe ser un numero positivo en mm."); return null; }
            if (!TryParse(_join.Text, out double join) || join < 0) { ShowError("La tolerancia de union debe ser un numero no negativo en mm."); return null; }
            if (!TryParse(_minArea.Text, out double minArea) || minArea < 0) { ShowError("El area minima debe ser un numero no negativo en m²."); return null; }
            _error.Visibility = Visibility.Collapsed;
            return new LosasSettings
            {
                CutLineMode = _select.IsChecked == true ? CutLineMode.Select : CutLineMode.Draw,
                ExtendCutLines = _extend.IsChecked == true,
                DeleteOriginal = _delete.IsChecked == true,
                CopyParameters = _copy.IsChecked == true,
                KeepJoins = _joins.IsChecked == true,
                KeepRebar = _rebar.IsChecked == true,
                TessellationToleranceMm = tess,
                JoinToleranceMm = join,
                MinAreaM2 = minArea
            };
        }

        private static bool TryParse(string text, out double value) =>
            double.TryParse(text?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        private void ShowError(string message)
        {
            _error.Text = message;
            _error.Visibility = Visibility.Visible;
        }
    }
}
