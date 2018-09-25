using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NugetLicenseRetriever.Lib
{
    public class LicenseResolver
    {
        private readonly SpdxLicenseData _spdxLicenseData;
        private readonly System.Net.Http.Headers.AuthenticationHeaderValue _authHeader;

        private LicenseResolver()
        {

        }

        public LicenseResolver(SpdxLicenseData spdxLicenseData, string githubUsername, string githubPassword)
        {
            _spdxLicenseData = spdxLicenseData;
            var byteArray = Encoding.ASCII.GetBytes(githubUsername + ":" + githubPassword);
            _authHeader = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        /// <summary>
        /// Try get accurate license data to match spdx.org
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task<Tuple<AccuracyOfLicense, SpdxLicense>> ResolveAsync(Uri uri)
        {
            //Try Github API if that returns nothing try default type.
            if (uri.AbsoluteUri.ToLower().Contains("github"))
            {
                var license = await DetermineGitHubLicenseAsync(uri);
                if (license?.Item2 == null || license.Item2.Id == "Unknown")
                {
                    license = await DetermineLicenseAsync(uri, license?.Item2?.Text);
                }
                return license;
            }

            string url = uri.AbsoluteUri;
            var response = await GetHttpAsync(url);

            //Grab license text and do a search.
            return await DetermineLicenseAsync(uri, response?.Item2);
        }

        private Task<Tuple<AccuracyOfLicense, SpdxLicense>> DetermineLicenseAsync(Uri uri, string content)
        {
            var result = new Tuple<AccuracyOfLicense, SpdxLicense>(
                AccuracyOfLicense.NotFound,
                new SpdxLicense
                {
                    Id = "Unknown",
                    Name = "Unknown",
                    KnownAliasUrls = new List<string> {uri.AbsoluteUri},
                    StandardLicenseTemplate = content.Replace("\r", "").Replace("\n", ""),
                    Text = content.Replace("\r", "").Replace("\n", ""),
                    SpdxDetailsUrl = "",
                    ReferenceNumber = "-1",
                    IsOsiApproved = false,
                    IsDeprecatedLicenseId = false
                });

            if (!string.IsNullOrEmpty(content))
            {
                foreach (var item in _spdxLicenseData.Licenses)
                {
                    //If we find High or PrettyLikely return right away as we are confident. Otherwise keeping looping hoping for a better match.
                    if (uri.AbsoluteUri.Contains(item.Id))
                    {
                        //Chance of being the correct license: High
                        return Task.FromResult(new Tuple<AccuracyOfLicense, SpdxLicense>(AccuracyOfLicense.High, item));
                    }

                    if (item.KnownAliasUrls.Contains(uri.AbsoluteUri))
                    {
                        //Chance of being the correct license: Pretty Likely
                        return Task.FromResult(new Tuple<AccuracyOfLicense, SpdxLicense>(AccuracyOfLicense.VeryLikely, item));
                    }

                    if (item.KnownAliasUrls.Any(content.Contains))
                    {
                        //Chance of being the correct license: decent chance
                        result = new Tuple<AccuracyOfLicense, SpdxLicense>(AccuracyOfLicense.DecentChance, item);
                    }

                    if (content.Contains(item.Name))
                    {
                        if (result.Item1 >= AccuracyOfLicense.Maybe)
                        {
                            //Chance of being the correct license: maybe
                            result = new Tuple<AccuracyOfLicense, SpdxLicense>(AccuracyOfLicense.Maybe, item);
                        }
                    }

                    //if (html.Contains(item.Id))
                    //{
                    //    return item;
                    //}
                }
            }

            return Task.FromResult(result);
        }

        private async Task<Tuple<AccuracyOfLicense, SpdxLicense>> DetermineGitHubLicenseAsync(Uri uri)
        {
            var jtoken = new JObject();
            string content = "";
            string[] matches = Regex.Split(uri.AbsolutePath, "/");
            if (matches.Any())
            {
                try
                {
                    var url = $"https://api.github.com/repos/{matches[1]}/{matches[2]}/license";

                    //Git hub license API:https://developer.github.com/v3/licenses/
                    //GET /repos/:owner/:repo/license
                    //Headers => Accept: application/vnd.github.v3+json
                    var response = await GetHttpAsync(
                        url,
                        new KeyValuePair<string, string>("Accept", "application/vnd.github.v3+json"),
                        _authHeader
                    );

                    if (!response.Item1)
                    {
                        return new Tuple<AccuracyOfLicense, SpdxLicense>(
                            AccuracyOfLicense.NotFound,
                            new SpdxLicense
                            {
                                Id = "Unknown",
                                Name = "Unknown",
                                KnownAliasUrls = new List<string>(),
                                StandardLicenseTemplate = response.Item2,
                                Text = response.Item2,
                                SpdxDetailsUrl = "",
                                ReferenceNumber = "-1",
                                IsOsiApproved = false,
                                IsDeprecatedLicenseId = false
                            });
                    }

                    jtoken = JObject.Parse(response.Item2);
                    var spdxid = jtoken["license"]["spdx_id"].ToString();

                    if (!string.IsNullOrEmpty(spdxid))
                    {
                        return new Tuple<AccuracyOfLicense, SpdxLicense>(AccuracyOfLicense.ExactMatchFound, _spdxLicenseData.Licenses.First(p => p.Id == spdxid));
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);

                    content = Encoding.UTF8.GetString(Convert.FromBase64String(jtoken["content"].ToString()));

                    return new Tuple<AccuracyOfLicense, SpdxLicense>(
                        AccuracyOfLicense.NotFound, 
                        new SpdxLicense
                        {
                            Id = "Unknown",
                            Name = "Unknown",
                            KnownAliasUrls = new List<string> {jtoken["download_url"].ToString()},
                            StandardLicenseTemplate = content,
                            Text = content,
                            SpdxDetailsUrl = "",
                            ReferenceNumber = "-1",
                            IsOsiApproved = false,
                            IsDeprecatedLicenseId = false
                        });
                }
            }

            content = Encoding.UTF8.GetString(Convert.FromBase64String(jtoken["content"].ToString()));

            return new Tuple<AccuracyOfLicense, SpdxLicense>(
                AccuracyOfLicense.NotFound,
                new SpdxLicense
                {
                    Id = "Unknown",
                    Name = "Unknown",
                    KnownAliasUrls = new List<string> { jtoken["download_url"].ToString() },
                    StandardLicenseTemplate = content,
                    Text = content,
                    SpdxDetailsUrl = "",
                    ReferenceNumber = "-1",
                    IsOsiApproved = false,
                    IsDeprecatedLicenseId = false
                });
        }

        /// <summary>
        /// Generic HTTP Helper
        /// </summary>
        /// <param name="url"></param>
        /// <param name="header"></param>
        /// <param name="authorization"></param>
        /// <returns>string</returns>
        private async Task<Tuple<bool, string>> GetHttpAsync(string url, KeyValuePair<string, string>? header = null, System.Net.Http.Headers.AuthenticationHeaderValue authorization = null)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "NugetLicenseRetriever.VisualStudio.Extension");

                    if (authorization != null)
                    {
                        client.DefaultRequestHeaders.Authorization = authorization;
                    }
                    
                    if (header.HasValue)
                    {
                        client.DefaultRequestHeaders.Add(header.Value.Key, header.Value.Value);
                    }
                    
                    //Flip over to different task so dont block current UI thread.
                    var result = await Task.Run(() => client.GetAsync(url));
                    //Flip over to different task so dont block current UI thread.
                    var content = await Task.Run(() => result.Content.ReadAsStringAsync());

                    if (result.IsSuccessStatusCode)
                    {
                        return new Tuple<bool, string>(true, content);
                    }

                    string message = "";
                    if (!string.IsNullOrEmpty(content))
                    {
                        //Github returns message in content even on failure.
                        var obj = JObject.Parse(content);
                        message = obj["message"].ToString();
                    }

                    return new Tuple<bool, string>(false, $"Response from '{url}' was not successful. ({result.ReasonPhrase}) " + message);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return new Tuple<bool, string>(false, e.Message);
            }
        }
    }
}
