using System;
using System.Collections.Generic;
using NuGet.Protocol;

namespace NugetLicenseRetriever.Lib
{
    public class PackageInfo
    {
        public LocalPackageInfo LocalPackageInfo { get; set; }
        public List<ProjectInfo> ProjectList { get; set; }
    }
}