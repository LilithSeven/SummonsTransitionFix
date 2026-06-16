using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Kingmaker.Localization;
using Kingmaker;

namespace SummonsTransitionFix
{
    public static class Localization
    {
        private static Dictionary<string, string> s_LocalisedStrings = new Dictionary<string, string>();
        private static Dictionary<string, string> s_FallbackStrings = new Dictionary<string, string>();
        private static string s_ModPath = string.Empty;

        public static void Init(string modPath)
        {
            s_ModPath = modPath;
            LoadFallback();
            UpdateLocale();
        }

        private static void LoadFallback()
        {
            try
            {
                string fallbackPath = Path.Combine(s_ModPath, "Localization", "enGB.json");
                if (File.Exists(fallbackPath))
                {
                    string json = File.ReadAllText(fallbackPath);
                    s_FallbackStrings = JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                }
            }
            catch (Exception)
            {
                
            }
        }

        public static void UpdateLocale()
        {
            if (Game.Instance == null)
            {
                
                s_LocalisedStrings = s_FallbackStrings;
                return;
            }

            try
            {
                string locale = LocalizationManager.CurrentLocale.ToString();
                string localePath = Path.Combine(s_ModPath, "Localization", $"{locale}.json");

                if (File.Exists(localePath))
                {
                    string json = File.ReadAllText(localePath);
                    s_LocalisedStrings = JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? s_FallbackStrings;
                }
                else
                {
                    s_LocalisedStrings = s_FallbackStrings;
                }
            }
            catch (Exception)
            {
                s_LocalisedStrings = s_FallbackStrings;
            }
        }

        public static string GetString(string key)
        {
            if (s_LocalisedStrings.TryGetValue(key, out string value))
            {
                return value;
            }
            if (s_FallbackStrings.TryGetValue(key, out value))
            {
                return value;
            }
            return key;
        }
    }
}