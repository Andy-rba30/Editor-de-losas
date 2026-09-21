using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ARBA.Losas.Revit.Settings
{
    /// <summary>
    /// Lee y escribe settings.json en %AppData%\ARBA\Losas\. Si el archivo no existe se usan
    /// los valores por defecto; si esta corrupto se informa y se usan tambien los valores
    /// por defecto (no se sobreescribe hasta que el usuario guarde).
    /// </summary>
    public static class SettingsStore
    {
        public static string Directory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ARBA", "Losas");

        public static string FilePath => Path.Combine(Directory, "settings.json");

        private static JsonSerializerOptions ReadOptions() => new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private static JsonSerializerOptions WriteOptions() => new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        /// <summary>Carga la configuracion. <paramref name="problem"/> describe por que se usaron los valores por defecto, o null.</summary>
        public static LosasSettings Load(out string problem)
        {
            problem = null;
            try
            {
                if (!File.Exists(FilePath)) return new LosasSettings();
                LosasSettings s = JsonSerializer.Deserialize<LosasSettings>(File.ReadAllText(FilePath), ReadOptions());
                return (s ?? new LosasSettings()).Sanitized();
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
            {
                problem = "No se pudo leer " + FilePath + " (" + ex.Message + "); se usan los valores por defecto.";
                return new LosasSettings();
            }
        }

        public static void Save(LosasSettings settings)
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings.Sanitized(), WriteOptions()));
        }
    }
}
