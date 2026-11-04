namespace SSMSPlusCore.Settings
{
    using System.Runtime.Serialization;

    /// <summary>The options of the Preferences window. A setting missing from the file keeps its default.</summary>
    [DataContract]
    public class UserPreferences
    {
        public UserPreferences()
        {
            SetDefaults();
        }

        /// <summary>Ask before executing an UPDATE / DELETE without WHERE or JOIN condition.</summary>
        [DataMember(Order = 1)]
        public bool ConfirmUnsafeStatements { get; set; }

        /// <summary>Show a message when an execution ends after <see cref="LongExecutionSeconds"/>.</summary>
        [DataMember(Order = 2)]
        public bool ShowLongExecutions { get; set; }

        /// <summary>Executions longer than this show a message.</summary>
        [DataMember(Order = 3)]
        public int LongExecutionSeconds { get; set; }

        /// <summary>The message is hidden after this time. 0: until closed.</summary>
        [DataMember(Order = 4)]
        public int LongExecutionHideAfterSeconds { get; set; }

        /// <summary>The message of executions longer than this stays until closed (<see cref="LongExecutionHideAfterSeconds"/> ignored). 0: never.</summary>
        [DataMember(Order = 5)]
        public int LongExecutionKeepAfterSeconds { get; set; }

        public UserPreferences Clone()
        {
            return (UserPreferences)MemberwiseClone();
        }

        // the serializer does not call the constructor
        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            SetDefaults();
        }

        private void SetDefaults()
        {
            ConfirmUnsafeStatements = true;
            ShowLongExecutions = true;
            LongExecutionSeconds = 60;
            LongExecutionHideAfterSeconds = 30;
            LongExecutionKeepAfterSeconds = 600;
        }
    }
}
