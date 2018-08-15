using System;
using System.IO;
using EnvDTE;

namespace NugetLicenseRetriever.VisualStudio.Extension
{
    public static class ProjectSettings
    {
        //private readonly DTE _env;

        //public ProjectSettings(EnvDTE.DTE env)
        //{
        //    _env = env;
        //}
        //public string FullPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), _env.RegistryRoot.Substring(9));
        //public string ReportFileName => Path.Combine(FullPath, "LicenseReport");
        //public string SpdxCacheFileName => Path.Combine(FullPath, "SpdxCache.json");
        //public string LicenseCacheFileName => Path.Combine(FullPath, Path.GetFileName(_env.Solution.FileName)?.Replace(".sln", "") + "_" + "LicenseCache.json");
        public static string ReportFileName { get; set; }
        public static string SpdxCacheFileName { get; set; }
        public static string LicenseCacheFileName { get; set; }
        internal const string DotnetCoreProjectGuid = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}";
        internal const string CollectionName = "NugetLicenseRetriever.VisualStudio.Extension";
        internal const string ReportGenerationOptionsDataKey = "ReportGenerationOptions";
    }
}
