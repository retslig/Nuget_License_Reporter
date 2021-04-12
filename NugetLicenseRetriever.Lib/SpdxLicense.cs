using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NugetLicenseRetriever.Lib
{
    public class SpdxLicenseData
    {
        [JsonProperty("licenses")]
        public List<SpdxLicense> Licenses { get; set; }
        [JsonProperty("licenseListVersion")]
        public decimal Version { get; set; }
    }

    public class SpdxLicense
    {
        /// <summary>
        /// SPDX identifier
        /// </summary>
        [JsonProperty("licenseId")]
        public string Id { get; set; }

        /// <summary>
        /// License full name
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// License text
        /// </summary>
        [JsonProperty("licenseText")]
        public string Text { get; set; }

        [JsonProperty("standardLicenseTemplate")]
        public string StandardLicenseTemplate { get; set; }

        [JsonProperty("isOsiApproved")]
        public bool IsOsiApproved { get; set; }

        [JsonProperty("seeAlso")]
        public List<string> KnownAliasUrls { get; set; }

        [JsonProperty("referenceNumber")]
        public string ReferenceNumber { get; set; }

        private string _spdxReferenceUrl;

        [JsonProperty("reference")]
        public string SpdxApiUrl
        {
            get { return _spdxReferenceUrl; }
            set
            {
                if (value != null && !value.Contains("https"))
                {
                    _spdxReferenceUrl = "https://spdx.org/licenses/" + value.Substring(2);
                }
                else { _spdxReferenceUrl = value; }
            }
        }

        [JsonProperty("detailsUrl")]
        public string SpdxDetailsUrl { get; set; }

        [JsonProperty("isDeprecatedLicenseId")]
        public bool IsDeprecatedLicenseId { get; set; }
    }
}