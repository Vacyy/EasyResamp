using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace EasyResamp
{
    public class UserSettings
    {
        public int DefaultWidth { get; set; } = 1920;
        public int DefaultHeight { get; set; } = 1080;
        public string OutputPath { get; set; } = "";
        public bool UseFixedPath { get; set; } = false;

        // --- ZMIANA ŚCIEŻKI ---

        // 1. Ustalamy folder w AppData (np. C:\Users\Kowalski\AppData\Roaming\EasyResampWPF)
        private static string SettingsFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EasyResampWPF");

        // 2. Pełna ścieżka do pliku
        private static string SettingsFilePath => Path.Combine(SettingsFolder, "settings.xml");

        public void Save()
        {
            try
            {
                // WAŻNE: Musimy upewnić się, że folder istnieje przed zapisem!
                if (!Directory.Exists(SettingsFolder))
                {
                    Directory.CreateDirectory(SettingsFolder);
                }

                XmlSerializer serializer = new XmlSerializer(typeof(UserSettings));
                using (StreamWriter writer = new StreamWriter(SettingsFilePath))
                {
                    serializer.Serialize(writer, this);
                }
            }
            catch (Exception ex)
            {
                // W wersji Release warto wiedzieć, jeśli zapis się nie uda (np. brak miejsca na dysku)
                System.Diagnostics.Debug.WriteLine("Błąd zapisu ustawień: " + ex.Message);
            }
        }

        public static UserSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(UserSettings));
                    using (StreamReader reader = new StreamReader(SettingsFilePath))
                    {
                        return (UserSettings)serializer.Deserialize(reader);
                    }
                }
            }
            catch
            {
                // Jeśli plik jest uszkodzony, zwracamy domyślne ustawienia
            }

            return new UserSettings();
        }
    }
}


