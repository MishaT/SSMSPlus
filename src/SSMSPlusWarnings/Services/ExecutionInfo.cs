namespace SSMSPlusWarnings.Services
{
    using System;

    /// <summary>A finished execution of a query window.</summary>
    public class ExecutionInfo
    {
        public DateTime Started { get; set; }

        public DateTime Ended { get; set; }

        public TimeSpan Duration => Ended - Started;

        /// <summary>Success, Failure, Cancelled, Timeout or Halted.</summary>
        public string Result { get; set; }

        /// <summary>For example "Dev.RMN_Back_Test (WinAuth)".</summary>
        public string Connection { get; set; }

        /// <summary>01:02:03, or 1.01:02:03 above a day.</summary>
        public static string FormatDuration(TimeSpan duration)
        {
            return duration.Days > 0 ? duration.ToString(@"d\.hh\:mm\:ss") : duration.ToString(@"hh\:mm\:ss");
        }
    }
}
