using System;
using System.IO;
using EnvDTE;

namespace NugetLicenseRetriever.VisualStudio.Extension
{
    public static class ProjectSettings
    {
        public static string ReportFileName { get; set; }
        public static string SpdxCacheFileName { get; set; }
        public static string LicenseCacheFileName { get; set; }
        internal const string DotnetCoreProjectGuid = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}";
        internal const string CollectionName = "NugetLicenseRetriever.VisualStudio.Extension";
        internal const string ReportGenerationOptionsDataKey = "ReportGenerationOptions";
    }
}
