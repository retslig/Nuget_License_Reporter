using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using Newtonsoft.Json;
using NugetLicenseRetriever.Lib;

//To be clear I have never done the wpf thing, so this is likely a disaster. 
//Refactoring will be happening at some point when I learn how to actually do the wpf. 
namespace NugetLicenseRetriever.VisualStudio.Extension
{
    using System.Diagnostics.CodeAnalysis;
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>
    /// Interaction logic for NuGetLicenseOptionsWindowControl.
    /// </summary>
    public partial class NuGetLicenseOptionsWindowControl : UserControl
    {
        private WritableSettingsStore _userSettingsStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="NuGetLicenseOptionsWindowControl"/> class.
        /// </summary>
        public NuGetLicenseOptionsWindowControl()
        {
            this.InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            var settingsManager = GetSettingsManagerAsync().Result;
            _userSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
            var reportGeneratorOptions = new ReportGeneratorOptions();

            if (_userSettingsStore.CollectionExists(ProjectSettings.CollectionName) &&
                _userSettingsStore.PropertyExists(ProjectSettings.CollectionName, ProjectSettings.ReportGenerationOptionsDataKey))
            {
                reportGeneratorOptions = JsonConvert.DeserializeObject<ReportGeneratorOptions>(
                    _userSettingsStore.GetString(ProjectSettings.CollectionName,
                        ProjectSettings.ReportGenerationOptionsDataKey)
                );
            }
            else
            {
                reportGeneratorOptions.FileType = FileType.Csv;
                reportGeneratorOptions.Path = ProjectSettings.ReportFileName;
                reportGeneratorOptions.Columns = typeof(LicenseRow).GetProperties().Select(p => p.Name).ToList();
            }

            //hack to make this work
            reportGeneratorOptions.Path = ProjectSettings.ReportFileName;

            var reportColunms = typeof(LicenseRow).GetProperties().ToList();
            int startingRow = 6;

            foreach (var column in reportColunms)
            {
                var checkbox = new CheckBox
                {
                    Content = column.Name,
                    Name = column.Name + "CheckBox",
                    Margin = new Thickness(10, 0, 0, 0),
                    IsChecked = reportGeneratorOptions?.Columns?.Any(p => p == column.Name)
                };

                Grid.SetColumn(checkbox, 0);
                Grid.SetRow(checkbox, startingRow);
                startingRow++;
                ReportColumnsGrid.Children.Add(checkbox);
            }

            switch (reportGeneratorOptions.FileType)
            {
                case FileType.Csv:
                    this.CsvRadioButton.IsChecked = true;
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
        }

       private void UpdateConfigurationSettingsStore(ReportGeneratorOptions reportGeneratorOptions)
        {
            if (!_userSettingsStore.CollectionExists(ProjectSettings.CollectionName))
            {
                _userSettingsStore.CreateCollection(ProjectSettings.CollectionName);
            }

            _userSettingsStore.SetString(
                ProjectSettings.CollectionName,
                ProjectSettings.ReportGenerationOptionsDataKey,
                JsonConvert.SerializeObject(reportGeneratorOptions)
            );
        }

        /// <summary>
        /// Handles click on the button by displaying a message box.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        private void Save_Button_Click(object sender, RoutedEventArgs e)
        {
            var licenseProperties = typeof(LicenseRow).GetProperties();
            var reportGeneratorOptions = new ReportGeneratorOptions {Columns = new List<string>()};
            foreach (CheckBox c in ReportColumnsGrid.Children.OfType<CheckBox>())
            {
                if (c.IsChecked == true)
                    reportGeneratorOptions.Columns.Add(c.Content.ToString());
            }

            foreach (RadioButton c in ReportColumnsGrid.Children.OfType<RadioButton>())
            {
                if (c.IsChecked == true)
                {
                    reportGeneratorOptions.FileType = (FileType)Enum.Parse(typeof(FileType), c.Content.ToString());
                    break;
                }
            }

            UpdateConfigurationSettingsStore(reportGeneratorOptions);

            var window = Window.GetWindow(this);
            window.Close();
        }

        private void Cancel_Button_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            window.Close();
            LoadSettings();
        }

        private void ClearCache_Button_Click(object sender, RoutedEventArgs e)
        {
            //if (_userSettingsStore.CollectionExists(ProjectConstants.CollectionName) &&
            //    _userSettingsStore.PropertyExists(ProjectConstants.CollectionName, ProjectConstants.ReportGenerationOptionsDataKey))
            //{
            //    _userSettingsStore.DeleteProperty(ProjectConstants.CollectionName, ProjectConstants.ReportGenerationOptionsDataKey);
            //}

            //Clear cache
            var fi = new FileInfo(ProjectSettings.ReportFileName);
            if (fi.Exists)
            {
                fi.Delete();
            }

            var fi1 = new FileInfo(ProjectSettings.SpdxCacheFileName);
            if (fi1.Exists)
            {
                fi1.Delete();
            }

            var fi2 = new FileInfo(ProjectSettings.LicenseCacheFileName);
            if (fi2.Exists)
            {
                fi2.Delete();
            }
        }

        private async Task<ShellSettingsManager> GetSettingsManagerAsync()
        {
#pragma warning disable VSTHRD010 
            // False-positive in Threading Analyzers. Bug tracked here https://github.com/Microsoft/vs-threading/issues/230
            var svc = await AsyncServiceProvider.GlobalProvider.GetServiceAsync(typeof(SVsSettingsManager)) as IVsSettingsManager;
#pragma warning restore VSTHRD010 

            return new ShellSettingsManager(svc);
        }
    }
}