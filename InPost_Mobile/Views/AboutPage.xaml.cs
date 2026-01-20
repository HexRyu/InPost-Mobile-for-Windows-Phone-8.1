using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources;
using Windows.UI.Xaml; // <--- Dodane dla RoutedEventArgs
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace InPost_Mobile.Views
{
    public sealed partial class AboutPage : Page
    {
        public AboutPage()
        {
            this.InitializeComponent();
            SetAppVersion();
        }

        private void SetAppVersion()
        {
            PackageVersion version = Package.Current.Id.Version;
            string versionString = string.Format("{0}.{1}.{2}.{3}",
                version.Major, version.Minor, version.Build, version.Revision);

            var loader = new ResourceLoader();
            string label = loader.GetString("TxtVersionLabel");
            VersionTextBlock.Text = string.Format("{0}: {1}", label, versionString);
        }

        // --- NOWY PRZYCISK ---
        private void Credits_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(CreditsPage));
        }
    }
}