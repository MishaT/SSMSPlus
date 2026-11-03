namespace SSMSPlusCore.Integration
{
    using Microsoft.SqlServer.Management.UI.VSIntegration;
    using Microsoft.VisualStudio.Shell;

    public class ServiceCacheIntegration : IServiceCacheIntegration
    {
        public void OpenScriptInNewWindow(string script)
        {
            ServiceCache.ScriptFactory.CreateNewBlankScript(Microsoft.SqlServer.Management.UI.VSIntegration.Editors.ScriptType.Sql);

            // ServiceCache.ExtensibilityModel exposes the SSMS 22 interop types (Microsoft.VisualStudio.Interop 18.0), use the DTE service instead
            var dte = (EnvDTE.DTE)Package.GetGlobalService(typeof(EnvDTE.DTE));
            EnvDTE.TextDocument doc = (EnvDTE.TextDocument)dte.ActiveDocument.Object(null);
            doc.EndPoint.CreateEditPoint().Insert(script);
        }
    }
}
