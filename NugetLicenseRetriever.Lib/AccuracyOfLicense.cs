using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetLicenseRetriever.Lib
{
    public enum AccuracyOfLicense
    {
        ExactMatchFound = 1,
        High = 2,
        VeryLikely = 3,
        DecentChance = 4,
        Maybe = 5,
        NotFound = 6
    }
}
