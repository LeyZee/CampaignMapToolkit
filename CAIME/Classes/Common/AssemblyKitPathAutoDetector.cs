using Microsoft.Win32;

namespace CAIME
{
    class AssemblyKitPathAutoDetector
    {
        private GameTemplate[] SupportedGamesList;
        private string[] AssKitIds =
        {
            "267180",       // Rome 2
            "343660",       // Attila
            "817480",       // Thrones of Britannia
            "463690",       // Warhammer 1
            "651460",       // Warhammer 2
            "1880380",      // Warhammer 3
            "1012260",      // Three Kingdoms
            "1356310",      // Troy
            "1937790",      // Pharaoh
            "2951670",      // Pharaoh Dynasties
        };

        private static string[] GameIds =
        {
            "214950",       // Rome 2
            "325610",       // Attila
            "712100",       // Thrones of Britannia
            "364360",       // Warhammer 1
            "594570",       // Warhammer 2
            "1142710",      // Warhammer 3
            "779340",       // Three Kingdoms
            "1099410",      // Troy
            "1937780",      // Pharaoh
            "2951630",      // Pharaoh Dynasties
        };

        private static string[] GameExeNames =
        {
            "Rome2",        // Rome 2
            "Attila",       // Attila
            "Thrones",      // Thrones of Britannia
            "Warhammer",    // Warhammer 1
            "Warhammer2",   // Warhammer 2
            "Warhammer3",   // Warhammer 3
            "ThreeKingdoms",// Three Kingdoms
            "Troy",         // Troy
            "Pharaoh",      // Pharaoh
            "Pharaoh",      // Pharaoh Dynasties
        };

        public AssemblyKitPathAutoDetector()
        {
            SupportedGamesList = new GameTemplate[(int)GameTemplate.Count];
            for (int i = 0; i < (int)GameTemplate.Count; ++i)
            {
                SupportedGamesList[i] = (GameTemplate)i;
            }
        }

        public void Scan()
        {
            foreach (var game in SupportedGamesList)
            {
                var asskitPath = PreferencesViewModel.Instance.GetAssKitPath(game);
                if (asskitPath != null && asskitPath.Length > 0)
                {
                    continue;
                }

                var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App " + AssKitIds[(int)game]);
                if (key != null)
                {
                    PreferencesViewModel.Instance.SetAssKitPath(game, key.GetValue("InstallLocation") + @"\assembly_kit");
                }
            }

            PreferencesViewModel.Instance.Save();
        }

        public static string DetectExecutable(GameTemplate game)
        {
            var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App " + GameIds[(int)game]);
            if (key != null)
            {
                return key.GetValue("InstallLocation") + $@"\{GameExeNames[(int)game]}.exe";
            }

            return null;
        }
    }
}
