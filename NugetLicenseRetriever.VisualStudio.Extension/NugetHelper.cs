using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using EnvDTE;
using NugetLicenseRetriever.Lib;
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

        public IEnumerable<Lib.PackageInfo> GetNugetPackages(IVsPackageInstallerServices installerServices, Solution envSolution, bool includePackageDependencies)
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
                                if (ProjectSettings.DotnetCoreProjectGuid == childProject.Kind)
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

            var packageList = new List<PackageInfo>();
            var installedPackages = installerServices?.GetInstalledPackages().ToList();

            if (installedPackages != null && installedPackages.Any())
            {
                foreach (var installedPackage in installedPackages)
                {
                    var projects = new List<ProjectInfo>();

                    projects.AddRange(
                        from Project envProject in allprojects
                        where !string.IsNullOrEmpty(envProject.FullName) &&
                              installerServices.IsPackageInstalled(envProject, installedPackage.Id)
                        select new ProjectInfo
                        {
                            Name = envProject.Name,
                            FrameworkName = envProject.Properties.Item("TargetFrameworkMoniker")?.Value.ToString()
                        }
                    );

                    var version = NuGet.Versioning.NuGetVersion.Parse(installedPackage.VersionString);
                    if (string.IsNullOrEmpty(installedPackage.InstallPath))
                    {
                        Debug.WriteLine("Package: " + installedPackage.Id + " has no install path.");
                    }
                    else
                    {
                        var type = LocalFolderUtility.GetLocalFeedType(installedPackage.InstallPath, _logger);
                        var package = BuildPackageInfo(type, installedPackage.InstallPath, installedPackage.Id, version, projects);
                        if (package != null)
                        {
                            packageList.Add(package);

                            if (includePackageDependencies)
                            {
                                var dependenciesPackageList = new List<PackageInfo>();
                                var frameworks = projects.Select(p => p.FrameworkName).ToList();
                                foreach (var dependencyGroup in package.LocalPackageInfo.Nuspec.GetDependencyGroups())
                                {
                                    if (frameworks.Contains(dependencyGroup.TargetFramework.DotNetFrameworkName))
                                    {
                                        foreach (var packageDependency in dependencyGroup.Packages)
                                        {
                                            var depVersion = NuGet.Versioning.NuGetVersion.Parse(packageDependency.VersionRange.OriginalString);
                                            var basePath = Path.GetFullPath(Path.Combine(installedPackage.InstallPath, @"..\..\"));
                                            var newPath = Path.Combine(basePath, packageDependency.Id, depVersion.ToString());
                                            var dependencyPackage = BuildPackageInfo(type, newPath, packageDependency.Id, depVersion, projects);
                                            if (dependencyPackage != null)
                                            {
                                                dependenciesPackageList.Add(dependencyPackage);
                                            }
                                            else
                                            {
                                                Debug.WriteLine($"Could not locate dependency package {packageDependency.Id} version {depVersion}...");
                                                _logger.Log(LogLevel.Warning, $"Could not locate dependency package {packageDependency.Id} version {depVersion}...");
                                            }
                                        }
                                    }
                                }

                                if (dependenciesPackageList.Any())
                                {
                                    packageList.AddRange(dependenciesPackageList);
                                }
                            }
                        }                            
                        else
                        {
                            Debug.WriteLine($"Could not locate package {installedPackage.Id} version {version}...");
                            _logger.Log(LogLevel.Warning, $"Could not locate package {installedPackage.Id} version {version}...");
                        }
                    }
                }
            }

            return packageList;
        }

        private PackageInfo BuildPackageInfo(FeedType type, string rootPath, string id, NuGet.Versioning.NuGetVersion version, List<ProjectInfo> projects)
        {
            LocalPackageInfo localPackageInfo;

            switch (type)
            {
                case FeedType.FileSystemV2:
                    localPackageInfo = LocalFolderUtility.GetPackageV2(rootPath, id, version, _logger);
                    if (localPackageInfo == null)
                    {
                        return null;
                    }
                    return new PackageInfo
                    {
                        ProjectList = projects,
                        LocalPackageInfo = localPackageInfo
                    };
                case FeedType.FileSystemV3:
                    localPackageInfo = LocalFolderUtility.GetPackageV3(rootPath, id, version, _logger);
                    if (localPackageInfo == null)
                    {
                        return null;
                    }
                    return
                        new PackageInfo
                        {
                            ProjectList = projects,
                            LocalPackageInfo = localPackageInfo
                        };
                default:
                {
                    Debug.WriteLine($"Unknown Type:{type}");
                    return null;
                }
            }
        }
    }
}
