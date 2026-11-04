namespace SSMSPlusWarnings.Checks
{
    using SSMSPlusWarnings.Services;

    /// <summary>
    /// A check run for the query windows, which shows or hides its warnings with the <see cref="WarningCenter"/>.
    /// To add a warning: implement this interface and register it in ServiceCollectionExtension.
    /// Called on the UI thread, while the window is not executing.
    /// </summary>
    public interface IQueryWindowCheck
    {
        void Check(QueryWindow window, QueryWindowEvent windowEvent);
    }
}
