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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
        private string _status;
        public string Status {
            get {
                return _status;
            }
            set {
                _status = value;
                this.OnPropertyChanged("Status");
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            Status = "Test";
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            TextBlock_AssemblyVersion.Text = "Version " + version;

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
                                            PropertyName = (string)f.Attribute("propertyname"),
                                            FriendlyPropertyName = (string)f.Attribute("friendlyname"),
                                            Notes = (string)f.Attribute("notes"),
                                            OriginalValue = f.Value

                                        }).ToList(),
                                        IntProperties = e.Elements("intproperty").Select(f => new IniPropertyInt
                                        {
                                            PropertyName = (string)f.Attribute("propertyname"),
                                            FriendlyPropertyName = (string)f.Attribute("friendlyname"),
                                            Notes = (string)f.Attribute("notes"),
                                            OriginalValue = f.Value
                                        }).ToList(),
                                        FloatProperties = e.Elements("floatproperty").Select(f => new IniPropertyFloat
                                        {
                                            PropertyName = (string)f.Attribute("propertyname"),
                                            FriendlyPropertyName = (string)f.Attribute("friendlyname"),
                                            Notes = (string)f.Attribute("notes"),
                                            OriginalValue = f.Value
                                        }).ToList(),
                                        EnumProperties = e.Elements("enumproperty").Select(f => new IniPropertyEnum
                                        {
                                            PropertyName = (string)f.Attribute("propertyname"),
                                            FriendlyPropertyName = (string)f.Attribute("friendlyname"),
                                            Notes = (string)f.Attribute("notes"),
                                            Choices = f.Elements("enumvalue").Select(g => new IniPropertyEnumValue
                                            {
                                                FriendlyName = (string)g.Attribute("friendlyname"),
                                                IniValue = g.Value
                                            }).ToList()
                                        }).ToList(),
                                        NameProperties = e.Elements("nameproperty").Select(f => new IniPropertyName
                                        {
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
                    IniFile configIni = new IniFile(inifilepath);
                    foreach (IniPropertyMaster prop in items)
                    {
                        prop.LoadCurrentValue(configIni);
                    }

                    //load console setting
                    if (File.Exists(configFileFolder + "\\BIOInput.ini"))
                    {
                        IniFile inputini = new IniFile(configFileFolder + "\\BIOInput.ini");
                        string key = inputini.Read("ConsoleKey", "Engine.Console");
                        Checkbox_EnableInGameConsole.IsChecked = key != "";
                        EnableConsole = Checkbox_EnableInGameConsole.IsChecked.Value;
                    }

                    kp.Key.ItemsSource = items;
                    CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(kp.Key.ItemsSource);
                    PropertyGroupDescription groupDescription = new PropertyGroupDescription("SectionFriendlyName");
                    view.GroupDescriptions.Add(groupDescription);
                }

            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Status = "Saving...";

            //hack to not block UI thread without doing a bunch of async and ui thread stuff.
            System.Threading.Timer timer = null;
            timer = new System.Threading.Timer((obj) =>
            {
                saveData();
                timer.Dispose();
            }, null, 100, System.Threading.Timeout.Infinite);


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
                if (File.Exists(configFileFolder + "\\" + kp.Value))
                {
                    IniFile ini = new IniFile(configFileFolder + "\\" + kp.Value);
                    foreach (IniPropertyMaster prop in kp.Key.Items)
                    {
                        ini.Write(prop.PropertyName, prop.ValueToWrite, prop.SectionName);
                        //Console.WriteLine("[" + prop.SectionName + "] " + prop.PropertyName + "=" + prop.ValueToWrite);
                    }
                }
            }

            if (File.Exists(configFileFolder + "\\BIOInput.ini"))
            {
                IniFile inputini = new IniFile(configFileFolder + "\\BIOInput.ini");
                string section = "Engine.Console";
                if (EnableConsole)
                {
                    inputini.Write("ConsoleKey", "Tilde", section);
                }
                else
                {
                    inputini.DeleteKey("ConsoleKey", section);
                }
            }
            Status = "Ini configuration saved";
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            Status = "Reset";
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
    }
}
