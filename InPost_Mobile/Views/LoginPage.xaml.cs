using System;
using System.Globalization;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.Popups;
using Windows.ApplicationModel.Resources;
using InPost_Mobile.Models;

namespace InPost_Mobile.Views
{
    public sealed partial class LoginPage : Page
    {
        private bool _isPl;

        public LoginPage()
        {
            this.InitializeComponent();
            string lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            _isPl = (lang == "pl");
            UpdateTexts();
        }

        private void UpdateTexts()
        {
            if (TbTerms != null) TbTerms.Inlines.Clear();

            if (_isPl)
            {
                LblPhone.Text = "Numer telefonu";
                BtnSendSms.Content = "WYŚLIJ KOD SMS";
                LblSms.Text = "Kod weryfikacyjny";
                BtnVerify.Content = "ZALOGUJ SIĘ";
                BtnBack.Content = "ZMIEŃ NUMER";
                BtnSkip.Content = "POMIŃ / SKIP";

                BuildTermsText("Przez zalogowanie się numerem telefonu zgadzam się na warunki użytkowania i informacje prawne zawarte w sekcji ",
                               "Informacje prawne i użytkowania");
            }
            else
            {
                LblPhone.Text = "Phone number";
                BtnSendSms.Content = "SEND SMS CODE";
                LblSms.Text = "Verification code";
                BtnVerify.Content = "LOG IN";
                BtnBack.Content = "CHANGE NUMBER";
                BtnSkip.Content = "SKIP LOGIN";

                BuildTermsText("By logging in with a phone number, I agree to the terms of use and legal information contained in the section ",
                               "Legal Information and Terms of Use");
            }
        }

        private void BuildTermsText(string prefix, string linkText)
        {
            if (TbTerms == null) return;

            TbTerms.Inlines.Add(new Run { Text = prefix });

            Hyperlink link = new Hyperlink();
            link.Foreground = new SolidColorBrush(Colors.Gray);
             // tekst linku
            Run linkRun = new Run { Text = linkText };
            link.Inlines.Add(linkRun);

            // Obsługa kliknięcia - nawigacja do LegalPage
            link.Click += (s, args) => { Frame.Navigate(typeof(LegalPage)); };

            TbTerms.Inlines.Add(link);
        }

        private void PhoneInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (PhoneInput.Text == "1724")
            {
               BtnSendSms.Content = "DEBUG TEST";
            }
            else
            {
               BtnSendSms.Content = _isPl ? "WYŚLIJ KOD SMS" : "SEND SMS CODE";
            }
        }

        private async void SendSms_Click(object sender, RoutedEventArgs e)
        {
            // DEBUG TRIGGER
            if (PhoneInput.Text == "1724")
            {
                ParcelManager.IsDebugMode = true;
                Frame.Navigate(typeof(MainPage));
                return;
            }

            // 1. Walidacja Zgody (Checkbox)
            if (CbTerms.IsChecked != true)
            {
                var loader = new ResourceLoader();
                var dialog = new MessageDialog(loader.GetString("Dialog_TermsRequiredContent"), loader.GetString("Dialog_TermsRequiredTitle"));
                await dialog.ShowAsync();
                return;
            }

            // 2. Walidacja Numeru
            string phone = PhoneInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(phone) || phone.Length < 9 || !IsDigitsOnly(phone))
            {
                StatusText.Text = _isPl ? "Błędny numer telefonu." : "Invalid phone number.";
                return;
            }

            // 3. Wysyłanie
            StatusText.Text = "";
            TogglePanel(StepPhone, false);
            LoginProgress.Visibility = Visibility.Visible;

            bool success = await ParcelManager.RequestSmsCode(phone);

            LoginProgress.Visibility = Visibility.Collapsed;
            TogglePanel(StepPhone, true);

            if (success)
            {
                StepPhone.Visibility = Visibility.Collapsed;
                StepSms.Visibility = Visibility.Visible;
                BtnSkip.Visibility = Visibility.Collapsed;
            }
            else
            {
                StatusText.Text = _isPl ? "Błąd połączenia. Spróbuj ponownie." : "Connection error. Try again.";
            }
        }

        private async void VerifySms_Click(object sender, RoutedEventArgs e)
        {
            string phone = PhoneInput.Text.Trim();
            string code = SmsInput.Text.Trim();

            if (code.Length < 6 || !IsDigitsOnly(code))
            {
                StatusText.Text = _isPl ? "Kod musi mieć 6 cyfr." : "Code must be 6 digits.";
                return;
            }

            TogglePanel(StepSms, false);
            LoginProgress.Visibility = Visibility.Visible;

            bool success = await ParcelManager.VerifySmsCode(phone, code);

            LoginProgress.Visibility = Visibility.Collapsed;
            TogglePanel(StepSms, true);

            if (success)
            {
                var settings = ApplicationData.Current.LocalSettings;
                settings.Values["IsSetupDone"] = true;
                Frame.Navigate(typeof(MainPage));
            }
            else
            {
                StatusText.Text = _isPl ? "Błędny kod SMS." : "Invalid SMS code.";
            }
        }

        private void BackToPhone_Click(object sender, RoutedEventArgs e)
        {
            StepSms.Visibility = Visibility.Collapsed;
            StepPhone.Visibility = Visibility.Visible;
            BtnSkip.Visibility = Visibility.Visible;
            StatusText.Text = "";
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["IsSetupDone"] = true;
            Frame.Navigate(typeof(MainPage));
        }

        private void TogglePanel(StackPanel panel, bool isEnabled)
        {
            panel.IsHitTestVisible = isEnabled;
            panel.Opacity = isEnabled ? 1.0 : 0.5;
        }

        private bool IsDigitsOnly(string str)
        {
            foreach (char c in str)
            {
                if (c < '0' || c > '9')
                    return false;
            }
            return true;
        }
    }
}