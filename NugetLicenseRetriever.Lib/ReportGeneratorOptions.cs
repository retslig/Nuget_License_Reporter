using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NugetLicenseRetriever.Lib
{
    public class ReportGeneratorOptions
    {
        public string Path { get; set; }
        public FileType FileType { get; set; }
        public List<string> Columns { get; set; }
        public bool IncludePackageDependencies { get; set; }
    }
}
