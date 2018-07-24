using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EnvDTE;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.VisualStudio;

namespace NugetLicenseRetriever.VisualStudio.Extension
{
    public class NugetHelper
    {
        private ILogger _logger;

        private NugetHelper()
        {
            
        }

        public NugetHelper(ILogger logger)
        {
            _logger = logger;
        }

        public Dictionary<LocalPackageInfo, List<string>> GetNugetPackageProjectDictionary(IVsPackageInstallerServices installerServices, Solution envSolution)
        {
            var allprojects = new List<Project>();

            if (envSolution != null)
            {
                foreach (Project project in envSolution.Projects)
                {
                    Debug.WriteLine("PojectName: " + project.Name);
                    if (EnvDTE.Constants.vsProjectKindSolutionItems == project.Kind)
                    {
                        foreach (ProjectItem item in project.ProjectItems)
                        {
                            Debug.WriteLine("PojectItemName: " + item.Name);
                            if (item.Object != null)
                            {
                                var childProject = item.Object as Project;
                                Debug.WriteLine("PojectItemName: " + item.Name + " ProjectItemGuid: " +
                                                childProject.Kind);
                                if (ProjectConstants.DotnetCoreProjectGuid == childProject.Kind)
                                {
                                    allprojects.Add(childProject);
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine("PojectName: " + project.Name + " PojectNameGuid: " + project.Kind);
                        allprojects.Add(project);
                    }
                }
            }

            if (!allprojects.Any())
            {
                Debug.WriteLine("No projects found.");
                return null;
            }

            var dictionary = new Dictionary<LocalPackageInfo, List<string>>();
            var installedPackages = installerServices?.GetInstalledPackages().ToList();

            if (installedPackages != null && installedPackages.Any())
            {
                foreach (var installedPackage in installedPackages)
                {
                    var projects = new List<string>();

                    projects.AddRange(
                        from Project envProject in allprojects
                        where !string.IsNullOrEmpty(envProject.FullName) &&
                              installerServices.IsPackageInstalled(envProject, installedPackage.Id)
                        select envProject.Name
                    );

                    var version = NuGet.Versioning.NuGetVersion.Parse(installedPackage.VersionString);
                    var type = LocalFolderUtility.GetLocalFeedType(installedPackage.InstallPath, _logger);

                    switch (type)
                    {
                        case FeedType.FileSystemV2:
                            dictionary.Add(
                                LocalFolderUtility.GetPackageV2(installedPackage.InstallPath, installedPackage.Id,
                                    version, _logger), projects);
                            break;
                        case FeedType.FileSystemV3:
                            dictionary.Add(
                                LocalFolderUtility.GetPackageV3(installedPackage.InstallPath, installedPackage.Id,
                                    version, _logger), projects);
                            break;
                        default:
                        {
                            Debug.WriteLine($"Unknown Type:{type}");
                            throw new NotImplementedException($"Unknown Type:{type}");
                        }
                    }
                }
            }

            return dictionary;
        }
    }
}
