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
        public static string GetSavedGamesPath()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, "Saved Games", "Frontier Developments", "Elite Dangerous");
        }
    }
}
