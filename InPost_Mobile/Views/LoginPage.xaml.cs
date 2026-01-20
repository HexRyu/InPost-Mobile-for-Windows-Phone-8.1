using System;
using System.Globalization;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
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
            if (_isPl)
            {
                LblPhone.Text = "Numer telefonu";
                BtnSendSms.Content = "WYŚLIJ KOD SMS";
                LblSms.Text = "Kod weryfikacyjny";
                BtnVerify.Content = "ZALOGUJ SIĘ";
                BtnBack.Content = "ZMIEŃ NUMER";
                BtnSkip.Content = "POMIŃ / SKIP";
            }
            else
            {
                LblPhone.Text = "Phone number";
                BtnSendSms.Content = "SEND SMS CODE";
                LblSms.Text = "Verification code";
                BtnVerify.Content = "LOG IN";
                BtnBack.Content = "CHANGE NUMBER";
                BtnSkip.Content = "SKIP LOGIN";
            }
        }

        private async void SendSms_Click(object sender, RoutedEventArgs e)
        {
            // 1. Czyścimy numer (usuwamy spacje i myślniki)
            string rawPhone = PhoneInput.Text;
            string phone = rawPhone.Replace(" ", "").Replace("-", "").Trim();

            // 2. Walidacja (musi być 9 cyfr dla Polski)
            if (phone.Length != 9 || !IsDigitsOnly(phone))
            {
                StatusText.Text = _isPl ? "Wpisz poprawny 9-cyfrowy numer." : "Enter valid 9-digit number.";
                return;
            }

            // BLOKOWANIE UI
            LoginProgress.Visibility = Visibility.Visible;
            StatusText.Text = "";
            TogglePanel(StepPhone, false);

            // Wywołujemy Managera (przekazujemy czyste 9 cyfr)
            bool sent = await ParcelManager.RequestSmsCode(phone);

            // ODBLOKOWANIE UI
            LoginProgress.Visibility = Visibility.Collapsed;
            TogglePanel(StepPhone, true);

            if (sent)
            {
                StepPhone.Visibility = Visibility.Collapsed;
                StepSms.Visibility = Visibility.Visible;
                BtnSkip.Visibility = Visibility.Collapsed;
                // Czyścimy pole kodu dla wygody
                SmsInput.Text = "";
            }
            else
            {
                StatusText.Text = _isPl ? "Błąd wysyłania. Sprawdź numer lub spróbuj później." : "Send error. Check number.";
            }
        }

        private async void VerifySms_Click(object sender, RoutedEventArgs e)
        {
            string code = SmsInput.Text.Trim();
            // Pobieramy numer ponownie i czyścimy, żeby mieć pewność
            string phone = PhoneInput.Text.Replace(" ", "").Replace("-", "").Trim();

            if (code.Length < 6)
            {
                StatusText.Text = _isPl ? "Kod musi mieć 6 cyfr." : "Code too short.";
                return;
            }

            // BLOKOWANIE UI
            LoginProgress.Visibility = Visibility.Visible;
            StatusText.Text = "";
            TogglePanel(StepSms, false);

            bool success = await ParcelManager.VerifySmsCode(phone, code);

            // ODBLOKOWANIE UI
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