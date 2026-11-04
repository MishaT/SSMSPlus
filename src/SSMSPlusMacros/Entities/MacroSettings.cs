namespace SSMSPlusMacros.Entities
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [DataContract]
    public class MacroSettings
    {
        [DataMember(Order = 1)]
        public List<MacroDefinition> Macros { get; set; } = new List<MacroDefinition>();

        /// <summary>SSMS key bindings removed because a macro took the key, given back when the macro releases it.</summary>
        [DataMember(Order = 2)]
        public List<DisplacedBinding> DisplacedBindings { get; set; } = new List<DisplacedBinding>();
    }

    [DataContract]
    public class DisplacedBinding
    {
        /// <summary>Command name, for display (some commands have none).</summary>
        [DataMember(Order = 1)]
        public string Command { get; set; }

        [DataMember(Order = 2)]
        public string CommandGuid { get; set; }

        [DataMember(Order = 3)]
        public int CommandId { get; set; }

        /// <summary>Visual Studio binding, e.g. "SQL Query Editor::F5".</summary>
        [DataMember(Order = 4)]
        public string Binding { get; set; }
    }
}
