using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace EliteInfoPanel.Core
{
    public static class EliteDangerousPaths
    {
        public static string GetSavedGamesPath(bool developmentMode = false)
        {
            if (developmentMode)
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var basePath = Path.Combine(userProfile, "Saved Games", "Frontier Developments", "Elite Dangerous");

                // Create dev folder if it doesn't exist
                var devPath = Path.Combine(basePath, "dev");
                Directory.CreateDirectory(devPath);

                return devPath;
            }

            var regPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(regPath, "Saved Games", "Frontier Developments", "Elite Dangerous");
        }
    }
}
