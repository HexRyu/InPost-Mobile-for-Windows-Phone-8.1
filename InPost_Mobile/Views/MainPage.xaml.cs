using System;
using System.Linq;
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
            // Handle Debug Button Visibility
            if (ParcelManager.IsDebugMode)
            {
                if (BtnDebug != null) BtnDebug.Visibility = Visibility.Visible;
                
                // Move Settings to Secondary to prevent overflow (5 items issue)
                if (MainCommandBar.PrimaryCommands.Contains(BtnSettings))
                {
                    MainCommandBar.PrimaryCommands.Remove(BtnSettings);
                    MainCommandBar.SecondaryCommands.Add(BtnSettings);
                }
            }
            else
            {
                if (BtnDebug != null) BtnDebug.Visibility = Visibility.Collapsed;

                // Move Settings back to Primary if needed
                if (MainCommandBar.SecondaryCommands.Contains(BtnSettings))
                {
                    MainCommandBar.SecondaryCommands.Remove(BtnSettings);
                    MainCommandBar.PrimaryCommands.Add(BtnSettings);
                }
            }

            if (_isFirstLoad)
            {
                if (ParcelManager.IsDebugMode && ParcelManager.AllParcels.Count == 0)
                {
                    // Ensure mocks are loaded if empty (e.g. first nav)
                     await ParcelManager.UpdateAllParcelsAsync();
                } 
                else if (!_isFirstLoad) 
                {
                    // ... standard refresh ...
                }
                
                if (!ParcelManager.IsDebugMode) await ParcelManager.LoadDataAsync();
                RefreshLists();
                _isFirstLoad = false;

                if (!ParcelManager.IsDebugMode)
                {
                    LoadingBar.Visibility = Visibility.Visible;
                    await ParcelManager.UpdateAllParcelsAsync();
                    RefreshLists();
                    LoadingBar.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                // Force update if coming from Login (New Navigation)
                if (e.NavigationMode == NavigationMode.New && !ParcelManager.IsDebugMode)
                {
                     LoadingBar.Visibility = Visibility.Visible;
                     await ParcelManager.UpdateAllParcelsAsync();
                     LoadingBar.Visibility = Visibility.Collapsed;
                }

                await ParcelManager.ReloadAllParcelsTranslation();
                RefreshLists();
            }
        }
        
        private void Debug_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(DebugPage));
        }

        private void RefreshLists()
        {
            RefreshReceiveList();
            if (_sendingList != null) _sendingList.ItemsSource = ParcelManager.GetActiveParcels("Send");
            if (_returnsList != null) _returnsList.ItemsSource = ParcelManager.GetActiveParcels("Return");
        }

        private void RefreshReceiveList()
        {
             var allReceive = ParcelManager.GetActiveParcels("Receive");
             var groups = new System.Collections.Generic.List<ParcelGroup>();
             var loader = new Windows.ApplicationModel.Resources.ResourceLoader();

             // 1. Ready for Pickup
             var readyName = loader.GetString("Section_ReadyForPickup");
             var readyParcels = allReceive.Where(p => ParcelManager.GetParcelSectionName(p.OriginalStatus) == readyName).ToList();
             if (readyParcels.Any()) groups.Add(new ParcelGroup(readyName, readyParcels));

             // 2. Out for Delivery
             var outName = loader.GetString("Section_OutForDelivery");
             var outParcels = allReceive.Where(p => ParcelManager.GetParcelSectionName(p.OriginalStatus) == outName).ToList();
             if (outParcels.Any()) groups.Add(new ParcelGroup(outName, outParcels));

             // 3. In Transit
             var transitName = loader.GetString("Section_InTransit");
             var transitParcels = allReceive.Where(p => ParcelManager.GetParcelSectionName(p.OriginalStatus) == transitName).ToList();
             if (transitParcels.Any()) groups.Add(new ParcelGroup(transitName, transitParcels));

             // 4. Delivered
             var deliveredName = loader.GetString("Section_Delivered");
             var deliveredParcels = allReceive.Where(p => ParcelManager.GetParcelSectionName(p.OriginalStatus) == deliveredName).ToList();
             if (deliveredParcels.Any()) groups.Add(new ParcelGroup(deliveredName, deliveredParcels));

             var cvs = this.Resources["ReceivedParcelsCVS"] as Windows.UI.Xaml.Data.CollectionViewSource;
             if (cvs != null) cvs.Source = groups;
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
            RefreshReceiveList();
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