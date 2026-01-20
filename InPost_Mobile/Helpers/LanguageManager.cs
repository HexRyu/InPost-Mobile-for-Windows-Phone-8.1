using System.Globalization;
using Windows.Globalization;
using Windows.Storage;

namespace InPost_Mobile.Helpers
{
    public static class LanguageManager
    {
        private const string LanguageKey = "AppLanguage";

        public static void InitializeLanguage()
        {
            var localSettings = ApplicationData.Current.LocalSettings;

            if (localSettings.Values.ContainsKey(LanguageKey))
            {
                string savedLang = localSettings.Values[LanguageKey].ToString();

                if (savedLang == "System")
                    ApplicationLanguages.PrimaryLanguageOverride = "";
                else
                    ApplicationLanguages.PrimaryLanguageOverride = savedLang;
            }
            else
            {
                string systemLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                if (systemLang == "pl")
                    ApplicationLanguages.PrimaryLanguageOverride = "pl-PL";
                else
                    ApplicationLanguages.PrimaryLanguageOverride = "en-US";
            }
        }

        public static void SetLanguage(string languageCode)
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[LanguageKey] = languageCode;

            if (languageCode == "System")
                ApplicationLanguages.PrimaryLanguageOverride = "";
            else
                ApplicationLanguages.PrimaryLanguageOverride = languageCode;
        }
    }
}