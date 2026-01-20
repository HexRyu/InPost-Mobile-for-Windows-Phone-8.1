using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using InPost_Mobile.Models;

namespace InPost_Mobile.Views
{
    public sealed partial class ArchivePage : Page
    {
        // Zmienna statyczna - pamięta wartość nawet jak wyjdziesz ze strony
        private static int _lastSectionIndex = 0;

        public ArchivePage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Po wejściu na stronę - przewiń do zapamiętanej sekcji
            if (ArchiveHub.Sections.Count > _lastSectionIndex)
            {
                ArchiveHub.ScrollToSection(ArchiveHub.Sections[_lastSectionIndex]);
            }
        }

        // --- ŁADOWANIE LIST (Bez zmian) ---
        private void ArchivedReceiveList_Loaded(object sender, RoutedEventArgs e)
        {
            var listView = sender as ListView;
            if (listView != null) listView.ItemsSource = ParcelManager.GetArchivedParcels("Receive");
        }

        private void ArchivedSendList_Loaded(object sender, RoutedEventArgs e)
        {
            var listView = sender as ListView;
            if (listView != null) listView.ItemsSource = ParcelManager.GetArchivedParcels("Send");
        }

        private void ArchivedReturnList_Loaded(object sender, RoutedEventArgs e)
        {
            var listView = sender as ListView;
            if (listView != null) listView.ItemsSource = ParcelManager.GetArchivedParcels("Return");
        }

        // --- KLIKNIĘCIE I ZAPAMIĘTANIE POZYCJI ---
        private void Parcel_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedParcel = e.ClickedItem as ParcelItem;
            if (clickedParcel != null)
            {
                switch (clickedParcel.ParcelType)
                {
                    case "Receive": _lastSectionIndex = 0; break;
                    case "Send": _lastSectionIndex = 1; break;
                    case "Return": _lastSectionIndex = 2; break;
                    default: _lastSectionIndex = 0; break;
                }

                // TU ZMIANA: Wysyłamy tylko numer
                Frame.Navigate(typeof(DetailsPage), clickedParcel.TrackingNumber);
            }
        }
    }
}