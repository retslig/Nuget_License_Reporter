using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using Newtonsoft.Json;
using System.Linq;
using NugetLicenseRetriever.Lib;
using NuGet.Common;
using NuGet.VisualStudio;
using Process = System.Diagnostics.Process;
using Task = System.Threading.Tasks.Task;

namespace NugetLicenseRetriever.VisualStudio.Extension
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class NugetLicenseReportGenerationCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        public string FullPath
        {
            get
            {
                var env = GetEnvAsync().Result;
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), env.RegistryRoot.Substring(9));
            }
        }

        public string ReportFileName => Path.Combine(FullPath, "LicenseReport");

        public string SpdxCacheFileName => Path.Combine(FullPath, "SpdxCache.json");

        public string LicenseCacheFileName
        {
            get
            {
                var env = GetEnvAsync().Result;
                return Path.Combine(FullPath, Path.GetFileName(env.Solution.FileName)?.Replace(".sln", "") + "_" + "LicenseCache.json");
            }
        }
        
        public const string CollectionName = "NugetLicenseRetriever.VisualStudio.Extension";
        public const string SpdxLicenseDataKey = "SpdxLicenseVersion";

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("e0cc967d-c96e-4866-8a6a-ee881cd83bb3");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="NugetLicenseReportGenerationCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private NugetLicenseReportGenerationCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandId = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandId);
            commandService.AddCommand(menuItem);
        }

        private void UpdateLicenseCache(Dictionary<string, LicenseRow> dictionary)
        {
            //json is to large to fit in settings store so store file path in the store then save the json file.
            var json = JsonConvert.SerializeObject(dictionary, Formatting.Indented);
            System.IO.File.WriteAllText(LicenseCacheFileName, json);
        }

        private Dictionary<string, LicenseRow> GetLicenseCache()
        {
            var fi = new FileInfo(LicenseCacheFileName);
            if (fi.Exists)
            {
                var json = System.IO.File.ReadAllText(fi.FullName);
                return JsonConvert.DeserializeObject<Dictionary<string, LicenseRow>>(json);
            }
            
            return new Dictionary<string, LicenseRow>();
        }


        private void UpdateConfigurationSettingsStoreForSpdxLicenseDataKey(WritableSettingsStore store, SpdxLicenseData spdxLicenseData)
        {
            //json is to large to fit in settings store so store file path in the store then save the json file.
            var json = JsonConvert.SerializeObject(spdxLicenseData,Formatting.Indented);
            System.IO.File.WriteAllText(SpdxCacheFileName, json);

            if (!store.CollectionExists(CollectionName))
            {
                store.CreateCollection(CollectionName);
            }
            
            store.SetString(CollectionName, SpdxLicenseDataKey, spdxLicenseData.Version.ToString());
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static NugetLicenseReportGenerationCommand Instance
        {
            get;
            private set;
        }

        private async Task<ShellSettingsManager> GetSettingsManagerAsync()
        {
#pragma warning disable VSTHRD010 
            // False-positive in Threading Analyzers. Bug tracked here https://github.com/Microsoft/vs-threading/issues/230
            var svc = await AsyncServiceProvider.GlobalProvider.GetServiceAsync(typeof(SVsSettingsManager)) as IVsSettingsManager;
#pragma warning restore VSTHRD010 

            return new ShellSettingsManager(svc);
        }

        private static async Task<IVsActivityLog> GetActivityLoggerAsync()
        {
            return await AsyncServiceProvider.GlobalProvider.GetServiceAsync(typeof(SVsActivityLog)) as IVsActivityLog;
        }

        private async Task<IVsPackageInstallerServices> GetIVsPackageInstallerServicesAsync()
        {
            var componentModel = this.package.GetServiceAsync(typeof(SComponentModel)).Result as IComponentModel;
            return componentModel?.GetService<IVsPackageInstallerServices>();
        }

        private async Task<EnvDTE.DTE> GetEnvAsync()
        {
            return this.package.GetServiceAsync(typeof(EnvDTE.DTE)).Result as EnvDTE.DTE;
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Verify the current thread is the UI thread - the call to AddCommand in NugetCommand's constructor requires
            // the UI thread.
            ThreadHelper.ThrowIfNotOnUIThread();

            OleMenuCommandService commandService = await package.GetServiceAsync((typeof(IMenuCommandService))) as OleMenuCommandService;
            Instance = new NugetLicenseReportGenerationCommand(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            var log = GetActivityLoggerAsync().Result;

            try
            {
                var logger = NullLogger.Instance;
                var env = GetEnvAsync().Result;
                var licenseCache = GetLicenseCache();
                var installerServices = GetIVsPackageInstallerServicesAsync().Result;
                var reportGenerator = new ReportGenerator(ReportFileName, FileType.Csv);

                //Remove old report
                reportGenerator.RemoveReport();

                //Get nuget packages
                var helper = new NugetHelper(logger);
                //Todo: add more file types support.
                var nugetPackageProjectDictionary = helper.GetNugetPackageProjectDictionary(installerServices, env?.Solution);

                //First check if any NuGet packages are installed.
                if (nugetPackageProjectDictionary != null && nugetPackageProjectDictionary.Any())
                {
                    //Get spdx licenses
                    var settingsManager = GetSettingsManagerAsync().Result;
                    var userSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
                    SpdxLicenseData spdxLicenseData;

                    if (userSettingsStore.CollectionExists(CollectionName) &&
                        userSettingsStore.PropertyExists(CollectionName, SpdxLicenseDataKey))
                    {
                        var version = decimal.Parse(userSettingsStore.GetString(CollectionName, SpdxLicenseDataKey));
                        var spdxHelper = new SpdxLicenseHelper(logger);
                        spdxLicenseData = spdxHelper.GetLicencesAsync(false);

                        if (version < spdxLicenseData.Version)
                        {
                            spdxLicenseData = spdxHelper.GetLicencesAsync(true);
                            UpdateConfigurationSettingsStoreForSpdxLicenseDataKey(userSettingsStore, spdxLicenseData);
                        }
                        else
                        {
                            var fi = new FileInfo(SpdxCacheFileName);
                            if (fi.Exists)
                            {
                                var json = System.IO.File.ReadAllText(SpdxCacheFileName);
                                spdxLicenseData = JsonConvert.DeserializeObject<SpdxLicenseData>(json);
                            }
                        }
                    }
                    else
                    {
                        //Query data. 
                        //Todo: this will take a while so do something to alert user.
                        var spdxHelper = new SpdxLicenseHelper(logger);
                        spdxLicenseData = spdxHelper.GetLicencesAsync(true);
                        UpdateConfigurationSettingsStoreForSpdxLicenseDataKey(userSettingsStore, spdxLicenseData);
                    }

                    reportGenerator.Generate(nugetPackageProjectDictionary, licenseCache, spdxLicenseData, typeof(LicenseRow).GetProperties().ToList());

                    UpdateLicenseCache(licenseCache);
                }
                else
                {
                    Debug.WriteLine("No installed Nuget packages.");
                }
            }
            catch (Exception exception)
            {
                Debug.Write(exception.Message);
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
                log.LogEntry(
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
                    (UInt32)__ACTIVITYLOG_ENTRYTYPE.ALE_ERROR,
                    this.ToString(),
                    exception.Message
                );

                throw;
            }

            ThreadHelper.ThrowIfNotOnUIThread();
            string message = "Nuget Package License Report Generated";
            string title = "Nuget License Report";

            // Show a message box to prove we were here
            VsShellUtilities.ShowMessageBox(
                this.package,
                message,
                title,
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}
