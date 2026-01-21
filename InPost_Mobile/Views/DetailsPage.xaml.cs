using System;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using InPost_Mobile.Models;
using ZXing;
using ZXing.Common;
using Windows.UI.ViewManagement;

namespace InPost_Mobile.Views
{
    public sealed partial class DetailsPage : Page
    {
        private ParcelItem _currentParcel;
        private ResourceLoader _loader = new ResourceLoader();

        public DetailsPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            try
            {
                string trackingNumber = e.Parameter as string;

                if (!string.IsNullOrEmpty(trackingNumber))
                {
                    _currentParcel = ParcelManager.AllParcels.FirstOrDefault(p => p.TrackingNumber == trackingNumber);
                    if (_currentParcel != null)
                    {
                        this.DataContext = _currentParcel;
                        _currentParcel.PropertyChanged += Parcel_PropertyChanged;
                        UpdateInterface();
                    }
                }
            }
            catch (Exception ex)
            {
                var dialog = new MessageDialog(_loader.GetString("ErrorStartContent") + "\n" + ex.Message, _loader.GetString("ErrorStartTitle"));
                await dialog.ShowAsync();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (_currentParcel != null)
            {
                _currentParcel.PropertyChanged -= Parcel_PropertyChanged;
            }
            base.OnNavigatedFrom(e);
        }

        private async void Parcel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "PickupCode" || e.PropertyName == "Status" || e.PropertyName == "PickupPointName")
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    UpdateInterface();
                });
            }
        }



        private async void OpenRemote_Click(object sender, RoutedEventArgs e)
        {
            var confirmDialog = new MessageDialog(_loader.GetString("Dialog_OpenConfirmContent"), _loader.GetString("Dialog_OpenConfirmTitle"));
            confirmDialog.Commands.Add(new UICommand(_loader.GetString("BtnYes"), async (cmd) =>
            {
                await ProcessRemoteOpening();
            }));
            confirmDialog.Commands.Add(new UICommand(_loader.GetString("BtnNo")));
            await confirmDialog.ShowAsync();
        }
        private async Task ProcessRemoteOpening()
        {
            string sessionUuid = null;
            var statusBar = StatusBar.GetForCurrentView();

            try
            {
                statusBar.ProgressIndicator.Text = "Skrytka zaniedługo się otworzy";
                await statusBar.ProgressIndicator.ShowAsync();
                sessionUuid = await LockerManager.ValidateAndOpenAsync(_currentParcel);
            }
            catch (Exception ex)
            {
                var failDialog = new MessageDialog("Nie udało się otworzyć skrytki:\n" + ex.Message, "Błąd otwierania");
                await failDialog.ShowAsync();
                return; 
            }
            finally
            {
                await statusBar.ProgressIndicator.HideAsync();
            }

            if (string.IsNullOrEmpty(sessionUuid)) return;

            bool isClosed = false;
            for (int i = 0; i < 24; i++) 
            {
                await Task.Delay(5000);
                isClosed = await LockerManager.IsLockerClosedAsync(sessionUuid);
                if (isClosed) break;
            }

            if (isClosed)
            {
                var takenDialog = new MessageDialog(_loader.GetString("Dialog_ParcelTakenContent"), _loader.GetString("Dialog_ParcelTakenTitle"));

                takenDialog.Commands.Add(new UICommand(_loader.GetString("Btn_NotTaken"), async (cmd) =>
                {
                    await ProcessRemoteOpening();
                }));

                takenDialog.Commands.Add(new UICommand(_loader.GetString("BtnYes"), async (cmd) =>
                {
                    await LockerManager.TerminateSessionAsync(sessionUuid);
                    await ParcelManager.UpdateSingleParcelAsync(_currentParcel.TrackingNumber);
                    UpdateInterface();
                }));

                await takenDialog.ShowAsync();
            }
            else
            {
                var timeoutDialog = new MessageDialog(_loader.GetString("Dialog_OpenTimeoutContent"));
                await timeoutDialog.ShowAsync();
            }
        }


        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            if (_currentParcel == null) return;
            var inputTextBox = new TextBox { Height = 32, Text = _currentParcel.CustomName ?? "" };
            var dialog = new ContentDialog { Title = _loader.GetString("Dialog_NameTitle"), Content = inputTextBox, PrimaryButtonText = _loader.GetString("Dialog_Save"), SecondaryButtonText = _loader.GetString("Dialog_Cancel") };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string newName = inputTextBox.Text.Trim();
                await ParcelManager.RenameParcel(_currentParcel.TrackingNumber, newName);
                _currentParcel.CustomName = newName;
                this.DataContext = null;
                this.DataContext = _currentParcel;
            }
        }

        private void UpdateInterface()
        {
            if (_currentParcel == null) return;
            UpdateButtonsState();

            if (CodeSection != null) CodeSection.Visibility = Visibility.Collapsed;

            if (_currentParcel.IsArchived) return;

            bool hasCode = !string.IsNullOrEmpty(_currentParcel.PickupCode) && _currentParcel.PickupCode != "---";
            if (!hasCode) return;

            bool showCode = _currentParcel.Status.Contains("Paczkomat") || _currentParcel.Status.Contains("Locker") ||
                            _currentParcel.Status == _loader.GetString("Status_ready_to_pickup") ||
                            _currentParcel.Status == _loader.GetString("Status_out_for_delivery");

            if (showCode)
            {
                if (CodeSection != null) CodeSection.Visibility = Visibility.Visible;
                GenerateQrImage();
            }
        }

        private void GenerateQrImage()
        {
            try
            {
                string userPhone = "";
                var localSettings = ApplicationData.Current.LocalSettings;
                if (localSettings.Values.ContainsKey("UserPhone"))
                {
                    userPhone = localSettings.Values["UserPhone"].ToString();
                    userPhone = userPhone.Replace(" ", "").Trim();
                }
                string qrContent = $"P|{userPhone}|{_currentParcel.PickupCode}";
                var barcodeWriter = new BarcodeWriter<WriteableBitmap> { Format = BarcodeFormat.QR_CODE, Options = new EncodingOptions { Height = 400, Width = 400, Margin = 0 } };
                var bitMatrix = barcodeWriter.Encode(qrContent);
                int width = bitMatrix.Width; int height = bitMatrix.Height;
                WriteableBitmap bitmap = new WriteableBitmap(width, height);
                using (var stream = bitmap.PixelBuffer.AsStream())
                {
                    byte[] pixels = new byte[width * height * 4];
                    int index = 0;
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            byte color = bitMatrix[x, y] ? (byte)0 : (byte)255;
                            pixels[index++] = color; pixels[index++] = color; pixels[index++] = color; pixels[index++] = 255;
                        }
                    }
                    stream.Write(pixels, 0, pixels.Length);
                }
                bitmap.Invalidate();
                if (QrImage != null) QrImage.Source = bitmap;
            }
            catch { }
        }

        private void UpdateButtonsState()
        {
            if (BtnArchive == null || BtnRestore == null) return;
            if (_currentParcel.IsArchived) { BtnArchive.Visibility = Visibility.Collapsed; BtnRestore.Visibility = Visibility.Visible; }
            else { BtnArchive.Visibility = Visibility.Visible; BtnRestore.Visibility = Visibility.Collapsed; }
        }

        private async void Restore_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new MessageDialog(_loader.GetString("DialogRestoreContent"), _loader.GetString("DialogRestoreTitle"));
            dialog.Commands.Add(new UICommand(_loader.GetString("BtnYes"), async (cmd) => { _currentParcel.IsArchived = false; await ParcelManager.ForceSave(); UpdateInterface(); if (Frame.CanGoBack) Frame.GoBack(); }));
            dialog.Commands.Add(new UICommand(_loader.GetString("BtnNo")));
            await dialog.ShowAsync();
        }

        private async void Archive_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new MessageDialog(_loader.GetString("DialogArchiveContent"), _loader.GetString("DialogArchiveTitle"));
            dialog.Commands.Add(new UICommand(_loader.GetString("BtnYes"), async (cmd) => { _currentParcel.IsArchived = true; await ParcelManager.ForceSave(); if (Frame.CanGoBack) Frame.GoBack(); }));
            dialog.Commands.Add(new UICommand(_loader.GetString("BtnNo")));
            await dialog.ShowAsync();
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new MessageDialog(_loader.GetString("DialogDeleteContent"), _loader.GetString("DialogDeleteTitle"));
            dialog.Commands.Add(new UICommand(_loader.GetString("BtnYes"), (cmd) => { ParcelManager.RemoveParcel(_currentParcel); if (Frame.CanGoBack) Frame.GoBack(); }));
            dialog.Commands.Add(new UICommand(_loader.GetString("BtnNo")));
            await dialog.ShowAsync();
        }


    }
}