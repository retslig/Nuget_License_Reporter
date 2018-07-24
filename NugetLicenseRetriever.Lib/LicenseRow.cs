namespace NugetLicenseRetriever.Lib
{
    public class LicenseRow
    {
        public string Id { get; set; }
        public string Project { get; set; }
        public string Component { get; set; }
        public string Version { get; set; }
        public string License { get; set; }
        public string LicenseUrl { get; set; }
        public string SpdxLicenseId { get; set; }
        public string LicenseText { get; set; }
        public AccuracyOfLicense AccuracyOfLicense { get; set; }
    }
}