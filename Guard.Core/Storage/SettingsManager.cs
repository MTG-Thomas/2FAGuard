using System.Text.Json;
using Guard.Core.Models;

namespace Guard.Core.Storage
{
    public class SettingsManager
    {
        private static readonly string settingsFilePath = Path.Combine(
            InstallationContext.GetAppDataFolderPath(),
            "settings"
        );

        private static AppSettings? settings;

        public static AppSettings Settings =>
            settings
            ?? throw new InvalidOperationException(
                "SettingsManager.Init must be called before accessing settings"
            );

        public static void Init()
        {
            AppSettings loadedSettings = new();
            if (File.Exists(settingsFilePath))
            {
                byte[] fileData = File.ReadAllBytes(settingsFilePath);
                string fileContent = System.Text.Encoding.UTF8.GetString(fileData);
                AppSettings? appSettings = JsonSerializer.Deserialize<AppSettings>(fileContent);
                if (appSettings != null)
                {
                    loadedSettings = appSettings;
                }
            }
            settings = loadedSettings;
        }

        public static async Task Save()
        {
            string fileContent = JsonSerializer.Serialize(Settings);
            byte[] fileData = System.Text.Encoding.UTF8.GetBytes(fileContent);
            await SafeFileWriter.SaveFileAsync(settingsFilePath, fileData);
        }
    }
}
