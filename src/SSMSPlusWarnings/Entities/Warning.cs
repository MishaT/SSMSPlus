namespace SSMSPlusWarnings.Entities
{
    using System;

    public enum WarningSeverity
    {
        Info,
        Warning,
        Critical,
    }

    /// <summary>
    /// A warning shown in the popup at the top right corner of SSMS.
    /// Showing a warning with the <see cref="Key"/> of a visible one updates it in place.
    /// </summary>
    public class Warning
    {
        /// <summary>Identifies the warning, for example "open-transaction:3" for the query window 3.</summary>
        public string Key { get; set; }

        public string Title { get; set; }

        public string Message { get; set; }

        public WarningSeverity Severity { get; set; } = WarningSeverity.Warning;

        /// <summary>Called when the warning is clicked, for example to show the query window. Optional.</summary>
        public Action Activate { get; set; }

        /// <summary>Hides the warning after this time. Null: shown until closed or hidden by its check.</summary>
        public TimeSpan? HideAfter { get; set; }

        /// <summary>The text put on the clipboard by the copy button.</summary>
        public string ToClipboardText()
        {
            return string.IsNullOrEmpty(Message) ? Title : Title + Environment.NewLine + Message;
        }
    }
}
