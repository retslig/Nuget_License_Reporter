using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NuGet.Protocol;

namespace NugetLicenseRetriever.Lib
{
    public class ReportGenerator
    {
        private string _path;
        private readonly FileType _type;

        public ReportGenerator(string path, FileType type)
        {
            switch (_type)
            {
                case FileType.Csv:
                    _path = path + ".csv";
                    break;
                case FileType.Html:
                    throw new NotImplementedException();
                case FileType.Pdf:
                    throw new NotImplementedException();
                case FileType.Word:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            _type = type;
        }

        public void Generate(IDictionary<LocalPackageInfo, List<string>> packageList, Dictionary<string, LicenseRow> cachedLicenses, SpdxLicenseData spdxLicenseData, List<PropertyInfo> columns)
        {
            var licenses = ToLicenseRows(packageList, cachedLicenses, spdxLicenseData);

            switch (_type)
            {
                case FileType.Csv:
                    using (var write = new StreamWriter(_path))
                    {
                        using (var helper = new CsvHelper.CsvWriter(write))
                        {
                            helper.Configuration.SanitizeForInjection = true;
                            helper.Configuration.QuoteAllFields = true;

                            //Write headers
                            foreach (var property in columns)
                            {
                                helper.WriteField(property.Name, true);
                            }
                            helper.NextRecord();

                            //Write values
                            foreach (var license in licenses)
                            {
                                foreach (var property in columns)
                                {
                                    var value = property.GetValue(license.Value, null);
                                    helper.WriteField(value?.ToString(), true);
                                }
                                helper.NextRecord();
                            }
                        }
                    }
                    break;
                case FileType.Html:
                    throw new NotImplementedException();
                case FileType.Pdf:
                    throw new NotImplementedException();
                case FileType.Word:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var proc = new Process();
            var finfo = new FileInfo(_path);
            if (finfo.Exists)
            {
                proc.StartInfo.FileName = finfo.FullName;
                proc.Start();
            }
        }

        public void RemoveReport()
        {
            var fi = new FileInfo(_path);
            if (fi.Exists)
            {
                fi.Delete();
            }
        }

        private Dictionary<string, LicenseRow> ToLicenseRows(IDictionary<LocalPackageInfo, List<string>> packageList, Dictionary<string, LicenseRow> cachedLicenses, SpdxLicenseData spdxLicenseData)
        {
            var licenseResolver = new LicenseResolver(spdxLicenseData, "", "");

            var rows = new Dictionary<string, LicenseRow>();
            foreach (var package in packageList)
            {
                var nugetId = package.Key.Nuspec.GetId() + " : " + package.Key.Nuspec.GetVersion().Version;
                var licenseRow = new LicenseRow
                {
                    Id = nugetId,
                    Component = !string.IsNullOrEmpty(package.Key.Nuspec.GetTitle()) ? package.Key.Nuspec.GetTitle() : package.Key.Nuspec.GetId(),
                    Project = string.Join(",", package.Value),
                    LicenseUrl = package.Key.Nuspec.GetLicenseUrl(),
                    License = "Unknown",
                    Version = package.Key.Nuspec.GetVersion().Version.ToString(),
                    LicenseText = "",
                    SpdxLicenseId = "",
                    AccuracyOfLicense = AccuracyOfLicense.NotFound
                };

                //Check cache first
                //TODO consider checking if url has changed?
                if (cachedLicenses.ContainsKey(nugetId) && cachedLicenses[nugetId].LicenseUrl == licenseRow.LicenseUrl)
                {
                    licenseRow.License = cachedLicenses[nugetId].License;
                }
                else
                {
                    //Go determine license.
                    if (!string.IsNullOrEmpty(licenseRow.LicenseUrl))
                    {
                        var licenseTuple = licenseResolver.Resolve(new Uri(licenseRow.LicenseUrl));
                        if (licenseTuple?.Item2 != null)
                        {
                            licenseRow.License = licenseTuple.Item2.Name;
                            licenseRow.SpdxLicenseId = licenseTuple.Item2.Id;
                            licenseRow.LicenseText = licenseTuple.Item2.Text;
                            licenseRow.AccuracyOfLicense = licenseTuple.Item1;

                            if (cachedLicenses.ContainsKey(nugetId))
                            {
                                cachedLicenses[nugetId] = licenseRow;
                            }
                            else if (!licenseRow.License.Equals("Unknown"))
                            {
                                cachedLicenses.Add(nugetId, licenseRow);
                            }
                            else
                            {
                                Debug.WriteLine("Unable to determine license for {licenseRow.LicenseUrl}.");
                            }
                        }
                        else
                        {
                            Debug.WriteLine("Unable to determine license for {licenseRow.LicenseUrl}.");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Unable to determine license for {licenseRow.LicenseUrl}.");
                    }
                }

                if (rows.ContainsKey(nugetId))
                {
                    rows[nugetId] = licenseRow;
                }
                else
                {
                    rows.Add(nugetId, licenseRow);
                }
            }

            return rows;
        }
    }
}
