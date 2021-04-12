using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NugetLicenseRetriever.Lib;
using NuGet.Common;

namespace NugetLicenseRetriever.Console
{
    class Program
    {
        static void Main(string[] args)
        {

            var helper = new SpdxLicenseHelper(NullLogger.Instance);
            var data = helper.GetLicencesAsync(false).Result;
        }
    }
}
