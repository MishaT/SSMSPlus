namespace SSMSPlusMacros.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.Shell;

    using SSMSPlusCore.Integration;

    using SSMSPlusMacros.Entities;

    /// <summary>
    /// Assigns the macro shortcuts to the macro slot commands (SSMSPlus.Macro1..10) through the DTE key bindings,
    /// which SSMS keeps in the user keyboard scheme. A key used by an SSMS command in the same scope is taken from
    /// that command and given back when no macro uses it anymore.
    /// </summary>
    public class MacroKeyBinder
    {
        public const int FirstSlotCommandId = 3011;

        private const string GlobalScopeName = "Global";
        private const string DefaultQueryEditorScopeName = "SQL Query Editor";

        private readonly PackageProvider _packageProvider;
        private readonly ILogger<MacroKeyBinder> _logger;
        private string _queryEditorScopeName;

        public MacroKeyBinder(PackageProvider packageProvider, ILogger<MacroKeyBinder> logger)
        {
            _packageProvider = packageProvider;
            _logger = logger;
        }

        /// <summary>Name of the key binding scope, as shown in Tools > Options > Keyboard.</summary>
        public string GetScopeName(string scope)
        {
            return scope == MacroDefinition.GlobalScope ? GlobalScopeName : GetQueryEditorScopeName();
        }

        /// <summary>Sets the shortcut of each macro slot command. Cheap: used at startup.</summary>
        public void ApplySlotBindings(IReadOnlyList<MacroDefinition> macros)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            for (int slot = 0; slot < MacroStore.MaxMacros; slot++)
            {
                var macro = slot < macros.Count ? macros[slot] : null;
                var wanted = GetBinding(macro) is string binding ? new object[] { binding } : new object[0];

                var command = _packageProvider.Dte2.Commands.Item(MenuHelper.CommandSet.ToString("B"), FirstSlotCommandId + slot);
                var current = GetBindings(command);
                if (current.Length != wanted.Length || !current.Zip(wanted, (a, b) => SameBinding((string)a, (string)b)).All(same => same))
                {
                    command.Bindings = wanted;
                }
            }
        }

        /// <summary>
        /// Gives back the keys no macro uses anymore, assigns the macro shortcuts and takes them from the SSMS commands
        /// using them in the same scope (also chords starting with them). Updates <see cref="MacroSettings.DisplacedBindings"/>.
        /// Scans every command: a few seconds.
        /// </summary>
        /// <returns>What changed, for the user.</returns>
        public IReadOnlyList<string> Apply(MacroSettings settings)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var messages = new List<string>();
            var commands = _packageProvider.Dte2.Commands;
            var wanted = settings.Macros.Select(GetBinding).Where(b => b != null).ToList();

            foreach (var displaced in settings.DisplacedBindings.ToList())
            {
                if (wanted.Any(w => Takes(w, displaced.Binding)))
                    continue;

                try
                {
                    var command = commands.Item(displaced.CommandGuid, displaced.CommandId);
                    var bindings = GetBindings(command).Cast<string>().ToList();
                    if (!bindings.Any(b => SameBinding(b, displaced.Binding)))
                    {
                        bindings.Add(displaced.Binding);
                        command.Bindings = bindings.Cast<object>().ToArray();
                    }
                    messages.Add($"{displaced.Binding} given back to {DisplayName(displaced.Command)}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not give {Binding} back to {Command}", displaced.Binding, displaced.Command);
                }

                settings.DisplacedBindings.Remove(displaced);
            }

            ApplySlotBindings(settings.Macros);

            if (wanted.Count == 0)
                return messages;

            foreach (EnvDTE.Command command in commands)
            {
                if (IsSlot(command))
                    continue;

                var bindings = GetBindings(command).Cast<string>().ToArray();
                if (bindings.Length == 0)
                    continue;

                var taken = bindings.Where(b => wanted.Any(w => Takes(w, b))).ToArray();
                if (taken.Length == 0)
                    continue;

                command.Bindings = bindings.Except(taken).Cast<object>().ToArray();
                foreach (var binding in taken)
                {
                    settings.DisplacedBindings.Add(new DisplacedBinding { Command = command.Name, CommandGuid = command.Guid, CommandId = command.ID, Binding = binding });
                    messages.Add($"{binding} taken from {DisplayName(command.Name)}");
                }
            }

            return messages;
        }

        /// <summary>Names of the commands that can be used in a macro.</summary>
        public IReadOnlyList<string> GetCommandNames()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var names = new List<string>();
            foreach (EnvDTE.Command command in _packageProvider.Dte2.Commands)
            {
                if (!string.IsNullOrEmpty(command.Name) && !IsSlot(command))
                    names.Add(command.Name);
            }
            return names.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private string GetBinding(MacroDefinition macro)
        {
            if (macro == null || string.IsNullOrWhiteSpace(macro.Shortcut))
                return null;
            return GetScopeName(macro.Scope) + "::" + macro.Shortcut.Trim();
        }

        /// <summary>The scope of the SSMS Query.Execute shortcuts (F5, Ctrl+E): the query editor.</summary>
        private string GetQueryEditorScopeName()
        {
            if (_queryEditorScopeName != null)
                return _queryEditorScopeName;

            _queryEditorScopeName = DefaultQueryEditorScopeName;
            try
            {
                var execute = _packageProvider.Dte2.Commands.Item("Query.Execute", -1);
                var scope = GetBindings(execute).Cast<string>().Select(ScopeOf)
                    .FirstOrDefault(s => s.Length > 0 && !string.Equals(s, GlobalScopeName, StringComparison.OrdinalIgnoreCase));
                if (scope != null)
                    _queryEditorScopeName = scope;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read the scope of Query.Execute, using {Scope}", _queryEditorScopeName);
            }

            return _queryEditorScopeName;
        }

        private bool IsSlot(EnvDTE.Command command)
        {
            return Guid.TryParse(command.Guid, out var guid) && guid == MenuHelper.CommandSet
                && command.ID >= FirstSlotCommandId && command.ID < FirstSlotCommandId + MacroStore.MaxMacros;
        }

        /// <summary>The macro binding takes the existing binding: same keys, or a chord starting with the macro keys, in the same scope.</summary>
        private static bool Takes(string macroBinding, string existingBinding)
        {
            if (!string.Equals(ScopeOf(macroBinding), ScopeOf(existingBinding), StringComparison.OrdinalIgnoreCase))
                return false;

            var macroKeys = KeysOf(macroBinding);
            var existingKeys = KeysOf(existingBinding);
            return Shortcut.AreEqual(macroKeys, existingKeys)
                || (Shortcut.IsChord(existingKeys) && Shortcut.AreEqual(macroKeys, Shortcut.FirstStroke(existingKeys)));
        }

        private static bool SameBinding(string a, string b)
        {
            return string.Equals(ScopeOf(a), ScopeOf(b), StringComparison.OrdinalIgnoreCase) && Shortcut.AreEqual(KeysOf(a), KeysOf(b));
        }

        private static object[] GetBindings(EnvDTE.Command command)
        {
            return command.Bindings as object[] ?? new object[0];
        }

        private static string ScopeOf(string binding)
        {
            var i = binding.IndexOf("::", StringComparison.Ordinal);
            return i < 0 ? string.Empty : binding.Substring(0, i);
        }

        private static string KeysOf(string binding)
        {
            var i = binding.IndexOf("::", StringComparison.Ordinal);
            return i < 0 ? binding : binding.Substring(i + 2);
        }

        private static string DisplayName(string commandName)
        {
            return string.IsNullOrEmpty(commandName) ? "an unnamed SSMS command" : commandName;
        }
    }
}
