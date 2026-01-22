using System;
using System.Globalization;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Globalization;
using InPost_Mobile.Models;

namespace InPost_Mobile.Views
{
    public sealed partial class SettingsPage : Page
    {
        private bool _isPl;

        public SettingsPage()
        {
            this.InitializeComponent();

            string lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            _isPl = (lang == "pl");

            var settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.ContainsKey("AppLanguage"))
            {
                string savedLang = settings.Values["AppLanguage"].ToString();
                foreach (ComboBoxItem item in LanguageComboBox.Items)
                {
                    if (item.Tag.ToString() == savedLang)
                    {
                        LanguageComboBox.SelectedItem = item;
                        break;
                    }
                }
            }
            else
            {
                LanguageComboBox.SelectedIndex = 0;
            }

            TranslateUI();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            UpdateAccountStatus();
        }

        private void UpdateAccountStatus()
        {
            if (ParcelManager.IsLoggedIn())
            {
                PanelLoggedIn.Visibility = Visibility.Visible;
                PanelLoggedOut.Visibility = Visibility.Collapsed;
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey("UserPhone"))
                    LblUserPhone.Text = ApplicationData.Current.LocalSettings.Values["UserPhone"].ToString();
            }
            else
            {
                PanelLoggedIn.Visibility = Visibility.Collapsed;
                PanelLoggedOut.Visibility = Visibility.Visible;
            }
        }

        private void TranslateUI()
        {
            if (!_isPl)
            {
                // Tutaj można dodać ręczne tłumaczenia jeśli x:Uid nie wystarcza
                // Np. BtnLogout.Content = "LOG OUT";
            }
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LoginPage));
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            ParcelManager.Logout();
            UpdateAccountStatus();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(AboutPage));
        }

        // --- NOWY PRZYCISK: INFORMACJE PRAWNE ---
        private void Legal_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LegalPage));
        }

        private async void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            var item = combo.SelectedItem as ComboBoxItem;

            if (item != null)
            {
                var settings = ApplicationData.Current.LocalSettings;
                string selectedLang = item.Tag.ToString();

                if (settings.Values.ContainsKey("AppLanguage") && settings.Values["AppLanguage"].ToString() == selectedLang)
                    return;

                if (!settings.Values.ContainsKey("AppLanguage") && selectedLang == "System")
                    return;

                settings.Values["AppLanguage"] = selectedLang;
                ApplicationLanguages.PrimaryLanguageOverride = (selectedLang == "System") ? "" : selectedLang;

                // Fix: Force immediate translation of cached parcels
                await ParcelManager.ReloadAllParcelsTranslation();
            }
        }
    }
}