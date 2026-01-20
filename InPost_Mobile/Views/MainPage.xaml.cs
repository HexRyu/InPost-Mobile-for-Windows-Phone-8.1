using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.ApplicationModel;
using Windows.UI.Core;
using InPost_Mobile.Models;

namespace InPost_Mobile.Views
{
    public sealed partial class MainPage : Page
    {
        private ListView _parcelsList;
        private ListView _sendingList;
        private ListView _returnsList;

        private bool _isFirstLoad = true;
        private string _lastViewedTrackingNumber = null;

        public MainPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            ParcelManager.InitializeData();
            Application.Current.Resuming += App_Resuming;
        }

        private async void App_Resuming(object sender, object e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (LoadingBar != null) LoadingBar.Visibility = Visibility.Visible;
                await ParcelManager.UpdateAllParcelsAsync();
                RefreshLists();
                if (LoadingBar != null) LoadingBar.Visibility = Visibility.Collapsed;
            });
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (_isFirstLoad)
            {
                await ParcelManager.LoadDataAsync();
                RefreshLists();
                _isFirstLoad = false;

                LoadingBar.Visibility = Visibility.Visible;
                await ParcelManager.UpdateAllParcelsAsync();
                RefreshLists();
                LoadingBar.Visibility = Visibility.Collapsed;
            }
            else
            {

     
                await ParcelManager.ReloadAllParcelsTranslation();

                RefreshLists();
            }
        }

        private void RefreshLists()
        {
            if (_parcelsList != null) _parcelsList.ItemsSource = ParcelManager.GetActiveParcels("Receive");
            if (_sendingList != null) _sendingList.ItemsSource = ParcelManager.GetActiveParcels("Send");
            if (_returnsList != null) _returnsList.ItemsSource = ParcelManager.GetActiveParcels("Return");
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadingBar.Visibility = Visibility.Visible;
            await ParcelManager.UpdateAllParcelsAsync();
            RefreshLists();
            LoadingBar.Visibility = Visibility.Collapsed;
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage));
        }

        private void Archive_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ArchivePage));
        }

        // --- LISTY (Loaded) ---
        private void ParcelsList_Loaded(object sender, RoutedEventArgs e)
        {
            _parcelsList = sender as ListView;
            if (_parcelsList != null) _parcelsList.ItemsSource = ParcelManager.GetActiveParcels("Receive");
        }

        private void SendingList_Loaded(object sender, RoutedEventArgs e)
        {
            _sendingList = sender as ListView;
            if (_sendingList != null) _sendingList.ItemsSource = ParcelManager.GetActiveParcels("Send");
        }

        private void ReturnsList_Loaded(object sender, RoutedEventArgs e)
        {
            _returnsList = sender as ListView;
            if (_returnsList != null) _returnsList.ItemsSource = ParcelManager.GetActiveParcels("Return");
        }

        private async void AddParcel_Click(object sender, RoutedEventArgs e)
        {
            TextBox input = new TextBox { PlaceholderText = "Numer przesyłki" };


            ContentDialog dialog = new ContentDialog
            {
                Title = "Dodaj nową paczkę",
                Content = input,
                PrimaryButtonText = "Dodaj",
                SecondaryButtonText = "Anuluj"
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text))
            {
                bool success = await ParcelManager.AddParcelFromApi(input.Text);
                if (success) RefreshLists();
            }
        }

        // --- KLIKNIĘCIE W PACZKĘ ---
        private void Parcel_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedParcel = e.ClickedItem as ParcelItem;
            if (clickedParcel != null)
            {
                _lastViewedTrackingNumber = clickedParcel.TrackingNumber;
                Frame.Navigate(typeof(DetailsPage), clickedParcel.TrackingNumber);
            }
        }
    }
}