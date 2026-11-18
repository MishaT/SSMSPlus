namespace SSMSPlusVisualizers.Services
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;

    using Microsoft.Extensions.Logging;

    using SSMSPlusVisualizers.Entities;

    /// <summary>Saves a cell value to a temp file with the extension of the visualizer, then opens it.</summary>
    public class VisualizerRunner
    {
        private static readonly TimeSpan KeepFilesFor = TimeSpan.FromDays(1);

        private readonly ILogger<VisualizerRunner> _logger;

        public VisualizerRunner(ILogger<VisualizerRunner> logger)
        {
            _logger = logger;
        }

        /// <summary>%TEMP%\SSMS Plus\Visualizer, replaced in tests.</summary>
        public string Folder { get; set; } = Path.Combine(Path.GetTempPath(), "SSMS Plus", "Visualizer");

        /// <summary>Replaced in tests: starts the process.</summary>
        public Action<ProcessStartInfo> Start { get; set; } = info => Process.Start(info)?.Dispose();

        /// <summary>Returns the written file.</summary>
        public string Run(VisualizerDefinition visualizer, GridCell cell)
        {
            var path = WriteFile(cell.Value, cell.ColumnName, visualizer.Extension);
            Start(CreateStartInfo(visualizer, path));
            _logger.LogInformation("Visualizer {Name}: {Path}", visualizer.Name, path);
            return path;
        }

        public string WriteFile(object value, string columnName, string extension)
        {
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder, FileNameFor(columnName, extension, DateTime.Now));
            for (int i = 2; File.Exists(path); i++)
                path = Path.Combine(Folder, Path.GetFileNameWithoutExtension(FileNameFor(columnName, extension, DateTime.Now)) + "_" + i + NormalizeExtension(extension));

            if (value is byte[] bytes)
                File.WriteAllBytes(path, bytes);
            else
                File.WriteAllText(path, value as string ?? Convert.ToString(value, CultureInfo.CurrentCulture), new UTF8Encoding(false));

            return path;
        }

        /// <summary>"Photo_20261118_143005_123.png": the column name and a time stamp.</summary>
        public static string FileNameFor(string columnName, string extension, DateTime now)
        {
            var name = string.IsNullOrWhiteSpace(columnName) ? "value" : columnName.Trim();
            var invalid = Path.GetInvalidFileNameChars();
            name = new string(name.Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '_' : c).ToArray());
            if (name.Length > 60)
                name = name.Substring(0, 60);

            return name + "_" + now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + NormalizeExtension(extension);
        }

        /// <summary>".png" from "png", ".png" or " .PNG ".</summary>
        public static string NormalizeExtension(string extension)
        {
            var trimmed = (extension ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                return string.Empty;

            return trimmed.StartsWith(".") ? trimmed : "." + trimmed;
        }

        /// <summary>The application with its arguments, or the file itself (the Windows default application for the extension).</summary>
        public static ProcessStartInfo CreateStartInfo(VisualizerDefinition visualizer, string path)
        {
            if (string.IsNullOrWhiteSpace(visualizer.Application))
                return new ProcessStartInfo(path) { UseShellExecute = true };

            var arguments = string.IsNullOrWhiteSpace(visualizer.Arguments) ? VisualizerDefinition.DefaultArguments : visualizer.Arguments;
            if (!arguments.Contains(VisualizerDefinition.FilePlaceholder))
                arguments += " " + VisualizerDefinition.DefaultArguments;

            // shell execute: "mspaint" or "code" are found without their path
            var application = Environment.ExpandEnvironmentVariables(visualizer.Application.Trim().Trim('"'));
            return new ProcessStartInfo(application, arguments.Replace(VisualizerDefinition.FilePlaceholder, path))
            {
                UseShellExecute = true,
            };
        }

        /// <summary>The files are kept a day: they may contain sensitive data.</summary>
        public void DeleteOldFiles()
        {
            try
            {
                if (!Directory.Exists(Folder))
                    return;

                foreach (var file in Directory.GetFiles(Folder))
                {
                    try
                    {
                        if (DateTime.Now - File.GetLastWriteTime(file) > KeepFilesFor)
                            File.Delete(file);
                    }
                    catch (Exception)
                    {
                        // still open in its application: next time
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete the old visualizer files");
            }
        }
    }
}
