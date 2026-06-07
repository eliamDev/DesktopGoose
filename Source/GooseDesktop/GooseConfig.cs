using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace GooseDesktop
{
    public static class GooseConfig
    {
        public static void LoadConfig()
        {
            GooseConfig.settings = GooseConfig.ConfigSettings.ReadFileIntoConfig(GooseConfig.filePath);
        }

        private static string filePath = Program.GetPathToFileInAssembly("config.goos");

        public const int GOOSE_CONFIG_VERSION = 0;

        public static GooseConfig.ConfigSettings settings = null;

        public class ConfigSettings
        {
            public static GooseConfig.ConfigSettings ReadFileIntoConfig(string configGivenPath)
            {
                GooseConfig.ConfigSettings configSettings = new GooseConfig.ConfigSettings();
                if (!File.Exists(configGivenPath))
                {
                    MessageBox.Show("Can't find config.goos file! Creating a new one with default values");
                    GooseConfig.ConfigSettings.WriteConfigToFile(configGivenPath, configSettings);
                    return configSettings;
                }
                try
                {
                    using (StreamReader streamReader = new StreamReader(configGivenPath))
                    {
                        Dictionary<string, string> dictionary = new Dictionary<string, string>();
                        string text;
                        while ((text = streamReader.ReadLine()) != null)
                        {
                            string[] array = text.Split(new char[]
                            {
                                '='
                            });
                            if (array.Length == 2)
                            {
                                dictionary.Add(array[0], array[1]);
                            }
                        }
                        int num = -1;
                        int.TryParse(dictionary["Version"], out num);
                        if (num != 0)
                        {
                            MessageBox.Show("config.goos is for the wrong version! Creating a new one with default values!");
                            File.Delete(configGivenPath);
                            GooseConfig.ConfigSettings.WriteConfigToFile(configGivenPath, configSettings);
                            return configSettings;
                        }
                        foreach (KeyValuePair<string, string> keyValuePair in dictionary)
                        {
                            FieldInfo field = typeof(GooseConfig.ConfigSettings).GetField(keyValuePair.Key);
                            try
                            {
                                field.SetValue(configSettings, Convert.ChangeType(keyValuePair.Value, field.FieldType));
                            }
                            catch
                            {
                                MessageBox.Show("Loading config error: field " + field.Name + "'s value is not valid. Setting it to the default value.");
                            }
                        }
                    }
                }
                catch
                {
                    MessageBox.Show("config.goos corrupt! Creating a new one!");
                    File.Delete(configGivenPath);
                    GooseConfig.ConfigSettings.WriteConfigToFile(configGivenPath, configSettings);
                    return configSettings;
                }
                return configSettings;
            }

            public static void WriteConfigToFile(string path, GooseConfig.ConfigSettings f)
            {
                using (StreamWriter streamWriter = File.CreateText(path))
                {
                    streamWriter.Write(GooseConfig.ConfigSettings.GenerateTextFromSettings(f));
                }
            }

            public static string GenerateTextFromSettings(GooseConfig.ConfigSettings f)
            {
                StringBuilder stringBuilder = new StringBuilder();
                foreach (FieldInfo fieldInfo in typeof(GooseConfig.ConfigSettings).GetFields())
                {
                    stringBuilder.Append(string.Format("{0}={1}\n", fieldInfo.Name, fieldInfo.GetValue(f).ToString()));
                }
                return stringBuilder.ToString();
            }

            public int Version;

            public bool CanAttackAtRandom;

            public float MinWanderingTimeSeconds = 20f;

            public float MaxWanderingTimeSeconds = 40f;

            public float FirstWanderTimeSeconds = 20f;
        }
    }
}
