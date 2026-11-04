namespace SSMSPlusMacros.Entities
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [DataContract]
    public class MacroDefinition
    {
        public const string QueryEditorScope = "QueryEditor";
        public const string GlobalScope = "Global";

        [DataMember(Order = 1)]
        public string Name { get; set; }

        /// <summary>Visual Studio key notation, e.g. "F6" or "Ctrl+Shift+E". Empty: menu only.</summary>
        [DataMember(Order = 2)]
        public string Shortcut { get; set; }

        /// <summary><see cref="QueryEditorScope"/> or <see cref="GlobalScope"/>.</summary>
        [DataMember(Order = 3)]
        public string Scope { get; set; } = QueryEditorScope;

        /// <summary>SSMS command names executed in order, e.g. "Query.ResultsToText", "Query.Execute".</summary>
        [DataMember(Order = 4)]
        public List<string> Commands { get; set; } = new List<string>();
    }
}
