using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using InPost_Mobile.Models; // Explicitly confirming usage

namespace InPost_Mobile.Tasks
{
    public sealed class BackgroundSyncTask : IBackgroundTask
    {
        // Interwały (minuty) - Zoptymalizowane dla różnych statusów
        private const int INTERVAL_OUT_FOR_DELIVERY = 20;  // Wydana do doręczenia
        private const int INTERVAL_STANDARD = 90;           // Standardowe paczki (1.5h)
        private const int INTERVAL_READY_TO_PICKUP = 360;   // Gotowa do odbioru (6h)

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            BackgroundTaskDeferral deferral = null;
            if (taskInstance != null) deferral = taskInstance.GetDeferral();

            try
            {
                await RunLogicAsync(false);
            }
            catch { }
            finally
            {
                if (deferral != null) deferral.Complete();
            }
        }

        public async Task RunLogicAsync(bool force)
        {
                // Inicjalizacja danych
                await ParcelManager.LoadDataAsync();
                
                var settings = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
                
                long lastCheckOutForDelivery = GetLong(settings, "LastCheck_OutForDelivery");
                long lastCheckStandard = GetLong(settings, "LastCheck_Standard");
                long lastCheckReady = GetLong(settings, "LastCheck_Ready");

                DateTime now = DateTime.Now;
                long nowTicks = now.Ticks;

                bool checkOutForDelivery = force || (nowTicks - lastCheckOutForDelivery) >= TimeSpan.FromMinutes(INTERVAL_OUT_FOR_DELIVERY).Ticks;
                bool checkStandard = force || (nowTicks - lastCheckStandard) >= TimeSpan.FromMinutes(INTERVAL_STANDARD).Ticks;
                bool checkReady = force || (nowTicks - lastCheckReady) >= TimeSpan.FromMinutes(INTERVAL_READY_TO_PICKUP).Ticks;

                // 1. Out For Delivery (20 min) - Najwyższy priorytet
                var outForDeliveryParcels = ParcelManager.AllParcels.Where(p => IsOutForDelivery(p)).ToList();
                if (outForDeliveryParcels.Count > 0 && checkOutForDelivery)
                {
                    bool anyUpdated = false;
                    foreach (var p in outForDeliveryParcels)
                    {
                        string oldStatus = p.Status;
                        if (await ParcelManager.RefreshSingleParcel(p))
                        {
                            anyUpdated = true;
                            if (p.Status != oldStatus || force) HandleStatusChange(p, force);
                        }
                    }
                    if (anyUpdated) ParcelManager.ShouldUIUpdate = true;
                    settings["LastCheck_OutForDelivery"] = nowTicks;
                }

                // 2. Standard (1.5h) - Pozostałe aktywne paczki
                var standardParcels = ParcelManager.AllParcels.Where(p => IsStandardParcel(p)).ToList();
                if (standardParcels.Count > 0 && checkStandard)
                {
                    bool anyUpdated = false;
                    foreach (var p in standardParcels)
                    {
                        string oldStatus = p.Status;
                        if (await ParcelManager.RefreshSingleParcel(p))
                        {
                            anyUpdated = true;
                            if (p.Status != oldStatus || force) HandleStatusChange(p, force);
                        }
                    }
                    if (anyUpdated) ParcelManager.ShouldUIUpdate = true;
                    settings["LastCheck_Standard"] = nowTicks;
                }

                // 3. Ready To Pickup (6h) - Paczki gotowe do odbioru
                var readyParcels = ParcelManager.AllParcels.Where(p => IsReadyToPickup(p)).ToList();
                if (readyParcels.Count > 0 && checkReady)
                {
                    bool anyUpdated = false;
                    foreach (var p in readyParcels)
                    {
                        string oldStatus = p.Status;
                        if (await ParcelManager.RefreshSingleParcel(p))
                        {
                            anyUpdated = true;
                            if (p.Status != oldStatus || force) HandleStatusChange(p, force);
                        }
                    }
                    if (anyUpdated) ParcelManager.ShouldUIUpdate = true;
                    settings["LastCheck_Ready"] = nowTicks;
                }

                // 4. Discovery - Szukanie nowych paczek (tylko gdy zalogowany)
                if (ParcelManager.IsLoggedIn())
                {
                    await ParcelManager.UpdateAllParcelsAsync();
                }

                await ParcelManager.SaveDataAsync();
                UpdateLiveTile();
        }

        private long GetLong(Windows.Foundation.Collections.IPropertySet values, string key)
        {
            if (values.ContainsKey(key) && values[key] is long) return (long)values[key];
            return 0;
        }

        private bool IsOutForDelivery(ParcelItem p)
        {
            if (p.IsArchived || p.ParcelType != "Receive") return false;
            return p.OriginalStatus != null && p.OriginalStatus.ToLower().Contains("out_for_delivery");
        }

        private bool IsReadyToPickup(ParcelItem p)
        {
            if (p.IsArchived || p.ParcelType != "Receive") return false;
            string s = p.OriginalStatus?.ToLower() ?? "";
            return s.Contains("ready_to_pickup") || s.Contains("pickup_ready");
        }

        private bool IsStandardParcel(ParcelItem p)
        {
            if (p.IsArchived || ParcelManager.IsDeliveredStatus(p.Status)) return false;
            return !IsOutForDelivery(p) && !IsReadyToPickup(p);
        }

        private void HandleStatusChange(ParcelItem p, bool force = false)
        {
            if (p.ParcelType != "Receive") return;

            string s = p.OriginalStatus?.ToLower() ?? "";
            bool isOutForDelivery = s.Contains("out_for_delivery");
            bool isReady = s.Contains("ready_to_pickup") || s.Contains("pickup_ready");

            if (!isOutForDelivery && !isReady && !force) return;

            var loader = new Windows.ApplicationModel.Resources.ResourceLoader();
            string title = "InPost Mobile"; // Generic title
            string message = "";

            // Better formatting
            string senderOrNum = !string.IsNullOrEmpty(p.Sender) && p.Sender != "Nadawca" ? p.Sender : p.TrackingNumber;
            
            if (isOutForDelivery)
            {
                if (!string.IsNullOrEmpty(p.CustomName)) message = string.Format(loader.GetString("Notif_Out_Name"), p.CustomName);
                else message = string.Format(loader.GetString("Notif_Out_Sender"), senderOrNum);
            }
            else if (isReady)
            {
                 if (!string.IsNullOrEmpty(p.CustomName)) message = string.Format(loader.GetString("Notif_Ready_Name"), p.CustomName);
                 else message = string.Format(loader.GetString("Notif_Ready_Sender"), senderOrNum);
            }
            else if (force)
            {
                 message = $"Debug Status: {p.Status}";
            }

            if (!string.IsNullOrEmpty(message)) SendToast(title, message, p.TrackingNumber);
        }

        private void SendToast(string title, string content, string trackingNumber)
        {
            // Use ToastText04 for wrapping text (Title + 2 lines)
            var template = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText04);
            var textNodes = template.GetElementsByTagName("text");
            textNodes[0].InnerText = title;
            textNodes[1].InnerText = content;

            var toastNav = template.CreateAttribute("launch");
            toastNav.Value = trackingNumber; 
            template.SelectSingleNode("/toast").Attributes.SetNamedItem(toastNav);

            // Add Audio
            var audio = template.CreateElement("audio");
            audio.SetAttribute("src", "ms-winsoundevent:Notification.Default");
            template.SelectSingleNode("/toast").AppendChild(audio);

            var notifier = ToastNotificationManager.CreateToastNotifier();
            notifier.Show(new ToastNotification(template));
        }

        private void UpdateLiveTile()
        {
            TileManager.Update(ParcelManager.AllParcels);
        }
    }
}
