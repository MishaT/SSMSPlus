namespace SSMSPlusMacros.Services
{
    using System;
    using System.Runtime.InteropServices;

    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.Shell;

    using SSMSPlusCore.Integration;

    using SSMSPlusMacros.Entities;

    public class MacroRunner
    {
        private readonly PackageProvider _packageProvider;
        private readonly ILogger<MacroRunner> _logger;

        public MacroRunner(PackageProvider packageProvider, ILogger<MacroRunner> logger)
        {
            _packageProvider = packageProvider;
            _logger = logger;
        }

        /// <summary>
        /// Executes the commands of the macro in order, like the menu or a shortcut would.
        /// Stops at the first command that is not available (for example a query command outside a query window).
        /// </summary>
        public void Run(MacroDefinition macro)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = _packageProvider.Dte2;

            foreach (var line in macro.Commands)
            {
                // "Command.Name optional arguments"
                var commandLine = (line ?? string.Empty).Trim();
                if (commandLine.Length == 0)
                    continue;

                var separator = commandLine.IndexOf(' ');
                var commandName = separator < 0 ? commandLine : commandLine.Substring(0, separator);
                var arguments = separator < 0 ? string.Empty : commandLine.Substring(separator + 1).Trim();

                try
                {
                    dte.ExecuteCommand(commandName, arguments);
                }
                catch (COMException ex)
                {
                    _logger.LogWarning(ex, "Macro {Macro}: command {Command} failed", macro.Name, commandName);
                    dte.StatusBar.Text = $"SSMS Plus macro '{macro.Name}': '{commandName}' is not available here.";
                    return;
                }
                catch (ArgumentException ex)
                {
                    _logger.LogWarning(ex, "Macro {Macro}: unknown command {Command}", macro.Name, commandName);
                    dte.StatusBar.Text = $"SSMS Plus macro '{macro.Name}': unknown command '{commandName}'.";
                    return;
                }
            }
        }
    }
}
