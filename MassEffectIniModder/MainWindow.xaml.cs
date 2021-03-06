﻿/*=============================================
Copyright (c) 2018 ME3Tweaks
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
=============================================*/
using MassEffectIniModder.classes;
using Octokit;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Linq;

namespace MassEffectIniModder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private bool EnableConsole = false;

        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();

            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            TextBlock_AssemblyVersion.Text = "Version " + version;
            CheckForUpdates();

            string configFileFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\BioWare\Mass Effect\Config";
            if (Directory.Exists(configFileFolder))
            {
                List<KeyValuePair<ListView, string>> loadingMap = new List<KeyValuePair<ListView, string>>();
                loadingMap.Add(new KeyValuePair<ListView, string>(ListView_BIOEngine, "BioEngine.xml"));
                loadingMap.Add(new KeyValuePair<ListView, string>(ListView_BIOGame, "BioGame.xml"));
                loadingMap.Add(new KeyValuePair<ListView, string>(ListView_BIOParty, "BioParty.xml"));

                foreach (KeyValuePair<ListView, string> kp in loadingMap)
                {
                    XElement rootElement = XElement.Parse(GetPropertyMap(kp.Value));

                    var linqlist = (from e in rootElement.Elements("Section")
                                    select new IniSection
                                    {
                                        SectionName = (string)e.Attribute("name"),
                                        BoolProperties = e.Elements("boolproperty").Select(f => new IniPropertyBool
                                        {
                                            CanAutoReset = f.Attribute("canautoreset") != null ? (bool)f.Attribute("canautoreset") : true,
                                            PropertyName = (string)f.Attribute("propertyname"),
                                            FriendlyPropertyName = (string)f.Attribute("friendlyname"),
                                            Notes = (string)f.Attribute("notes"),
                                            OriginalValue = f.Value

                                        }).ToList(),
                                        IntProperties = e.Elements("intproperty").Select(f => new IniPropertyInt
                                        {
                                            CanAutoReset = f.Attribute("canautoreset") != null ? (bool)f.Attribute("canautoreset") : true,
                                            PropertyName = (string)f.Attribute("propertyname"),
                                            FriendlyPropertyName = (string)f.Attribute("friendlyname"),
                                            Notes = (string)f.Attribute("notes"),
                                            OriginalValue = f.Value
                                        }).ToList(),
                                        FloatProperties = e.Elements("floatproperty").Select(f => new IniPropertyFloat
                                        {
                                            CanAutoReset = f.Attribute("canautoreset") != null ? (bool)f.Attribute("canautoreset") : true,
                                            PropertyName = (string)f.Attribute("propertyname"),
                                            FriendlyPropertyName = (string)f.Attribute("friendlyname"),
                                            Notes = (string)f.Attribute("notes"),
                                            OriginalValue = f.Value
                                        }).ToList(),
                                        EnumProperties = e.Elements("enumproperty").Select(f => new IniPropertyEnum
                                        {
                                            CanAutoReset = f.Attribute("canautoreset") != null ? (bool)f.Attribute("canautoreset") : true,
                                            PropertyName = (string)f.Attribute("propertyname"),
                                            FriendlyPropertyName = (string)f.Attribute("friendlyname"),
                                            Notes = (string)f.Attribute("notes"),
                                            Choices = f.Elements("enumvalue").Select(g => new IniPropertyEnumValue
                                            {
                                                FriendlyName = (string)g.Attribute("friendlyname"),
                                                Notes = (string)g.Attribute("notes"),
                                                IniValue = g.Value
                                            }).ToList()
                                        }).ToList(),
                                        NameProperties = e.Elements("nameproperty").Select(f => new IniPropertyName
                                        {
                                            CanAutoReset = f.Attribute("canautoreset") != null ? (bool)f.Attribute("canautoreset") : true,
                                            PropertyName = (string)f.Attribute("propertyname"),
                                            FriendlyPropertyName = (string)f.Attribute("friendlyname"),
                                            Notes = (string)f.Attribute("notes"),
                                            OriginalValue = f.Value
                                        }).ToList(),

                                    }).ToList();

                    List<IniPropertyMaster> items = new List<IniPropertyMaster>();
                    foreach (IniSection sec in linqlist)
                    {
                        sec.PropogateOwnership();
                        items.AddRange(sec.GetAllProperties());
                    }

                    string inifilepath = configFileFolder + "\\" + System.IO.Path.GetFileNameWithoutExtension(kp.Value) + ".ini";
                    if (File.Exists(inifilepath))
                    {


                        IniFile configIni = new IniFile(inifilepath);
                        foreach (IniPropertyMaster prop in items)
                        {
                            prop.LoadCurrentValue(configIni);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Mass Effect Config file " + System.IO.Path.GetFileNameWithoutExtension(kp.Value) + ".ini is missing. It should be located at " + inifilepath + ". Please run the game at least once to generate the default files.");
                        Environment.Exit(1);
                    }


                    kp.Key.ItemsSource = items;
                    CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(kp.Key.ItemsSource);
                    PropertyGroupDescription groupDescription = new PropertyGroupDescription("SectionFriendlyName");
                    view.GroupDescriptions.Add(groupDescription);
                }
            }
            else
            {
                MessageBox.Show("Mass Effect Config directory is missing. It should be located at " + configFileFolder + ". Please run the game at least once to generate the default files.");
                Environment.Exit(1);
            }
            //load console setting
            string binputini = configFileFolder + "\\BIOInput.ini";
            if (File.Exists(binputini))
            {
                IniFile inputini = new IniFile(configFileFolder + "\\BIOInput.ini");
                string key = inputini.Read("ConsoleKey", "Engine.Console");
                Checkbox_EnableInGameConsole.IsChecked = key != "";
                EnableConsole = Checkbox_EnableInGameConsole.IsChecked.Value;
            }
            else
            {
                MessageBox.Show("Mass Effect Config file BioInput.ini is missing. It should be located at " + binputini + ". Please run the game at least once to generate the default files.");
                Environment.Exit(1);
            }
        }

        private async void CheckForUpdates()
        {
            var versInfo = System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
            var client = new GitHubClient(new ProductHeaderValue("MassEffectIniModder"));
            try
            {
                var releases = await client.Repository.Release.GetAll("Mgamerz", "MassEffectIniModder");
                if (releases.Count > 0)
                {
                    //The release we want to check is always the latest, so [0]
                    Release latest = null;
                    Version latestVer = new Version("0.0.0.0");
                    foreach (Release r in releases)
                    {
                        Version releaseVersion = new Version(r.TagName);
                        if (releaseVersion > latestVer && r.Assets.Count() > 0)
                        {
                            latest = r;
                            latestVer = releaseVersion;
                        }
                    }

                    //is there a latest release?
                    if (latest != null)
                    {
                        Version releaseName = new Version(latest.TagName);
                        if (versInfo < releaseName && latest.Assets.Count > 0)
                        {
                            bool upgrade = false;
                            var result = MessageBox.Show("An update is available. Update now?", "Update available", MessageBoxButton.YesNo);
                            upgrade = result == MessageBoxResult.Yes;
                            if (upgrade)
                            {
                                //there's an update
                                ShowMessage("Downloading update...", -1);
                                WebClient downloadClient = new WebClient();

                                downloadClient.Headers["Accept"] = "application/vnd.github.v3+json";
                                downloadClient.Headers["user-agent"] = "MassEffectIniModder";
                                string temppath = System.IO.Path.GetTempPath();
                                downloadClient.DownloadFileCompleted += ApplyUpdate;
                                downloadClient.DownloadProgressChanged += UpdateDownloadProgressChanged;
                                string downloadPath = temppath + "MassEffectIniModder_Update.zip";
                                //DEBUG ONLY
                                Uri downloadUri = new Uri(latest.Assets[0].BrowserDownloadUrl);
                                downloadClient.DownloadFileAsync(downloadUri, downloadPath, downloadPath);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                //don't do anything
            }
        }

        private void UpdateDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            TextBlock_Status.Text = "Downloading update " + e.ProgressPercentage + "%";
        }

        private void ApplyUpdate(object sender, AsyncCompletedEventArgs e)
        {
            string path = (string)e.UserState;
            var executinglocation = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\";
            try
            {
                if (Directory.Exists(executinglocation + "Update"))
                {
                    Directory.Delete(executinglocation + "Update", true);
                }
                Directory.CreateDirectory(executinglocation + "Update");
                ZipFile.ExtractToDirectory(path, executinglocation + "Update");
                Process.Start(executinglocation + @"Update\MassEffectIniModder.exe", "-updatemode");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                ShowMessage("Error occured while downloading or extracting update", 10000);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var timer = new DispatcherTimer();
            TextBlock_Status.Text = "Saving...";
            timer.Interval = TimeSpan.FromMilliseconds(20);
            bool run = true;
            timer.Tick += new EventHandler(async (object s, EventArgs a) =>
            {
                if (run)
                {
                    run = false;
                    await Task.Run(() => saveData());
                    ShowMessage("Ini configuration saved");
                    timer.Stop();
                }
            });
            timer.Start();
        }

        internal void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void saveData()
        {
            List<KeyValuePair<ListView, string>> saveMap = new List<KeyValuePair<ListView, string>>();
            saveMap.Add(new KeyValuePair<ListView, string>(ListView_BIOEngine, "BioEngine.ini"));
            saveMap.Add(new KeyValuePair<ListView, string>(ListView_BIOGame, "BioGame.ini"));
            saveMap.Add(new KeyValuePair<ListView, string>(ListView_BIOParty, "BioParty.ini"));
            string configFileFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\BioWare\Mass Effect\Config";

            foreach (KeyValuePair<ListView, string> kp in saveMap)
            {
                string file = configFileFolder + "\\" + kp.Value;
                if (File.Exists(file))
                {
                    File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);

                    IniFile ini = new IniFile(configFileFolder + "\\" + kp.Value);
                    foreach (IniPropertyMaster prop in kp.Key.Items)
                    {
                        string validation = prop.Validate("CurrentValue");
                        if (validation == null)
                        {
                            ini.Write(prop.PropertyName, prop.ValueToWrite, prop.SectionName);
                            //Console.WriteLine("[" + prop.SectionName + "] " + prop.PropertyName + "=" + prop.ValueToWrite);
                        }
                        else
                        {
                            MessageBox.Show("Property not saved: " + prop.FriendlyPropertyName + "\n\nReason: " + validation);
                        }
                    }
                    File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);
                }
            }
            string inputinipath = configFileFolder + "\\BIOInput.ini";
            if (File.Exists(inputinipath))
            {
                File.SetAttributes(inputinipath, File.GetAttributes(inputinipath) & ~FileAttributes.ReadOnly);

                IniFile inputini = new IniFile(configFileFolder + "\\BIOInput.ini");
                string section = "Engine.Console";
                if (EnableConsole)
                {
                    Console.WriteLine("Saving ocnsole");
                    inputini.Write("ConsoleKey", "Tilde", section);
                }
                else
                {
                    inputini.DeleteKey("ConsoleKey", section);
                }
                File.SetAttributes(inputinipath, File.GetAttributes(inputinipath) | FileAttributes.ReadOnly);
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            ListView[] lists = { ListView_BIOEngine, ListView_BIOGame, ListView_BIOParty };
            List<IniPropertyMaster> items = new List<IniPropertyMaster>();
            foreach (ListView lv in lists)
            {
                foreach (IniPropertyMaster lvi in lv.Items)
                {
                    items.Add(lvi);
                }
            }
            foreach (IniPropertyMaster prop in items)
            {
                if (prop.CanAutoReset)
                {
                    prop.Reset();
                }
            }
            ShowMessage("Items have been reset to default values (except basic settings)");
        }

        /// <summary>
        /// Shows a message in the statusbar, which is cleared after a few seconds.
        /// </summary>
        /// <param name="v">String to display</param>
        private void ShowMessage(string v, long milliseconds = 4000)
        {
            TextBlock_Status.Text = v;
            if (milliseconds > 0)
            {
                var timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(milliseconds);
                timer.Tick += new EventHandler((object s, EventArgs a) =>
                {
                    TextBlock_Status.Text = "";
                    timer.Stop();
                });
                timer.Start();
            }
        }

        public string GetPropertyMap(string filename)
        {
            string result = string.Empty;

            using (Stream stream = this.GetType().Assembly.
                       GetManifestResourceStream("MassEffectIniModder.propertymaps." + filename))
            {
                using (StreamReader sr = new StreamReader(stream))
                {
                    result = sr.ReadToEnd();
                }
            }
            return result;
        }

        private void Checkbox_EnableInGameConsole_Click(object sender, RoutedEventArgs e)
        {
            EnableConsole = Checkbox_EnableInGameConsole.IsChecked.Value;
        }

        private void Image_ME3Tweaks_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("https://me3tweaks.com");
            }
            catch (Exception ex)
            {

            }
        }
    }
}
