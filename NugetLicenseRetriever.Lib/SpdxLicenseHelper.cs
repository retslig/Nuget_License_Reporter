using NuGet.Common;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NugetLicenseRetriever.Lib
{
    public class SpdxLicenseHelper
    {
        private readonly ILogger _log;

        //Don't allow empty constructor
        private SpdxLicenseHelper()
        {
            
        }

        public SpdxLicenseHelper(ILogger log)
        {
            _log = log;
        }
        
        /// <summary>
        /// Useful links on API for SPDC.org
        /// https://spdx.org/sites/cpstandard/files/pages/files/spdx-tr-2014-2_v1_0_access_license_list.pdf
        /// https://github.com/spdx/license-list-data/blob/master/accessingLicenses.md
        /// </summary>
        private string _url = "https://spdx.org/licenses/licenses.json";

        /// <summary>
        /// Download licenses from spdx.org
        /// </summary>
        /// <param name="getIndividualLicenseText"></param>
        /// <returns>SpdxLicenseData</returns>
        public SpdxLicenseData GetLicencesAsync(bool getIndividualLicenseText)
        {
            var licenses = new SpdxLicenseData();
            try
            {
                var json = GetHttpAsync(_url);
                if (string.IsNullOrEmpty(json))
                {
                    throw new Exception($"No data returned from {_url}.");
                }

                licenses = LoadLicenses(json, getIndividualLicenseText);
            }
            catch (Exception ex)
            {
                _log.LogError($"Error occurred when downloading licenses from spdx.org. ({ex.Message})");
            }

            //Adding Amazon Web services licensing.
            licenses.Licenses.Add(new SpdxLicense
            {
                Id = "Apache License Version 2.0, January 2004",
                Name = "Apache License Version 2.0, January 2004",
                KnownAliasUrls = new List<string>
                {
                    "http://aws.amazon.com/apache2.0/"
                },
                StandardLicenseTemplate = "",
                Text = "",
                SpdxDetailsUrl = "",
                IsDeprecatedLicenseId = false,
                IsOsiApproved = false,
                ReferenceNumber = "-1"
            });

            //Add Microsoft licensing terms see list here for Visual Studio. https://visualstudio.microsoft.com/license-terms/

            //MICROSOFT SOFTWARE LICENSE TERMS
            //MICROSOFT VISUAL STUDIO 2017 TOOLS, ADD - ONs and EXTENSIONS
            licenses.Licenses.Add(new SpdxLicense
            {
                Id = "MICROSOFT VISUAL STUDIO 2017 TOOLS, ADD - ONs and EXTENSIONS",
                Name = "MICROSOFT VISUAL STUDIO 2017 TOOLS, ADD - ONs and EXTENSIONS",
                KnownAliasUrls = new List<string>
                {
                    "https://aka.ms/pexunj",
                    "https://visualstudio.microsoft.com/license-terms/mlt552233/"
                },
                StandardLicenseTemplate = "",
                Text = "",
                SpdxDetailsUrl = "",
                IsDeprecatedLicenseId = false,
                IsOsiApproved = false,
                ReferenceNumber = "-1"
            });

            //Web API
            //MICROSOFT SOFTWARE LICENSE TERMS
            //MICROSOFT.NET LIBRARY
            licenses.Licenses.Add(new SpdxLicense
            {
                Id = "MICROSOFT.NET LIBRARY",
                Name = "MICROSOFT.NET LIBRARY",
                KnownAliasUrls = new List<string>
                {
                    "http://www.microsoft.com/web/webpi/eula/net_library_eula_enu.htm"
                },
                StandardLicenseTemplate = "",
                Text = "",
                SpdxDetailsUrl = "",
                IsDeprecatedLicenseId = false,
                IsOsiApproved = false,
                ReferenceNumber = "-1"
            });

            //MICROSOFT SOFTWARE LICENSE TERMS
            //MICROSOFT.NET LIBRARY
            licenses.Licenses.Add(new SpdxLicense
            {
                Id = "MICROSOFT.NET LIBRARY",
                Name = "MICROSOFT.NET LIBRARY",
                KnownAliasUrls = new List<string>
                {
                    "http://go.microsoft.com/fwlink/?LinkId=329770",
                    "https://www.microsoft.com/net/dotnet_library_license.htm"
                },
                StandardLicenseTemplate = "",
                Text = "",
                SpdxDetailsUrl = "",
                IsDeprecatedLicenseId = false,
                IsOsiApproved = false,
                ReferenceNumber = "-1"
            });

            //MICROSOFT VISUAL STUDIO 2015 SOFTWARE DEVELOPMENT KIT
            licenses.Licenses.Add(new SpdxLicense
            {
                Id = "MICROSOFT VISUAL STUDIO 2015 SOFTWARE DEVELOPMENT KIT",
                Name = "MICROSOFT VISUAL STUDIO 2015 SOFTWARE DEVELOPMENT KIT",
                KnownAliasUrls = new List<string>
                {
                    "http://go.microsoft.com/fwlink/?LinkID=614949",
                    "https://visualstudio.microsoft.com/license-terms/mt171586/"
                },
                StandardLicenseTemplate = "",
                Text = "",
                SpdxDetailsUrl = "",
                IsDeprecatedLicenseId = false,
                IsOsiApproved = false,
                ReferenceNumber = "-1"
            });

            //MICROSOFT PRE-RELEASE SOFTWARE LICENSE TERMS
            //MICROSOFT VISUAL STUDIO 2017 FAMILY PRE-RELEASE SOFTWARE
            licenses.Licenses.Add(new SpdxLicense
            {
                Id = "MICROSOFT VISUAL STUDIO 2017 FAMILY PRE-RELEASE SOFTWARE",
                Name = "MICROSOFT VISUAL STUDIO 2017 FAMILY PRE-RELEASE SOFTWARE",
                KnownAliasUrls = new List<string>
                {
                    "https://go.microsoft.com/fwlink/?LinkID=746386",
                    "https://visualstudio.microsoft.com/license-terms/mt591984/"
                },
                StandardLicenseTemplate = "",
                Text = "",
                SpdxDetailsUrl = "",
                IsDeprecatedLicenseId = false,
                IsOsiApproved = false,
                ReferenceNumber = "-1"
            });

            return licenses;
        }

        /// <summary>
        /// Generic HTTP Helper
        /// </summary>
        /// <param name="url"></param>
        /// <returns>string</returns>
        private string GetHttpAsync(string url)
        {
            using (var client = new HttpClient())
            {
                //Flip over to different task so dont block current UI thread.
                var task = Task.Run(() => client.GetAsync(url));
                var res = task.Result;
                if (res.IsSuccessStatusCode)
                {
                    //Flip over to different task so dont block current UI thread.
                    return Task.Run(() => res.Content.ReadAsStringAsync()).Result;
                }

                throw new Exception($"Response from '{url}' was not successful. ({res.ReasonPhrase})");
            }
        }

        /// <summary>
        /// Parse licenses.json
        /// </summary>
        /// <param name="json"></param>
        /// <param name="getIndividualLicenseText"></param>
        private SpdxLicenseData LoadLicenses(string json, bool getIndividualLicenseText)
        {
            var licenseData = JsonConvert.DeserializeObject<SpdxLicenseData>(json);

            if (getIndividualLicenseText)
            {
                foreach (var license in licenseData.Licenses)
                {
                    var tempJson = GetHttpAsync(license.SpdxDetailsUrl);
                    var jtoken = JObject.Parse(tempJson);
                    license.Text = jtoken["licenseText"].ToString();
                    license.StandardLicenseTemplate = jtoken["standardLicenseTemplate"].ToString();
                }
            }

            return licenseData;
        }
    }
}
