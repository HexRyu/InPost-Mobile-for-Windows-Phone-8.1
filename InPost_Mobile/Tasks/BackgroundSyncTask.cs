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
        // Interwały (minuty)
        private const int INTERVAL_URGENT = 30;
        private const int INTERVAL_NORMAL = 90;
        private const int INTERVAL_DISCOVERY = 360;

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
                // Inicjalizacja danych localnych
                await ParcelManager.LoadDataAsync();
                
                // Pobierz czas ostatniego sprawdzenia z LocalSettings
                var settings = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
                
                long lastCheckUrgent = GetLong(settings, "LastCheck_Urgent");
                long lastCheckNormal = GetLong(settings, "LastCheck_Normal");
                long lastCheckDiscovery = GetLong(settings, "LastCheck_Discovery");

                DateTime now = DateTime.Now;
                long nowTicks = now.Ticks;

                bool checkUrgent = force || (nowTicks - lastCheckUrgent) >= TimeSpan.FromMinutes(INTERVAL_URGENT).Ticks;
                bool checkNormal = force || (nowTicks - lastCheckNormal) >= TimeSpan.FromMinutes(INTERVAL_NORMAL).Ticks;
                bool checkDiscovery = force || (nowTicks - lastCheckDiscovery) >= TimeSpan.FromMinutes(INTERVAL_DISCOVERY).Ticks;

                // 1. Priorytet (30 min): Paczki wydane do doręczenia
                var urgentParcels = ParcelManager.AllParcels.Where(p => IsOutForDelivery(p)).ToList();
                if (urgentParcels.Count > 0 && checkUrgent)
                {
                    foreach (var p in urgentParcels)
                    {
                        string oldStatus = p.Status;
                        // W trybie Debug/Force odświeżamy natychmiast
                        if (await ParcelManager.RefreshSingleParcel(p))
                        {
                            if (p.Status != oldStatus || force) HandleStatusChange(p, force); // W force wysyłamy powiadomienie nawet jeśli status ten sam (do testów)
                        }
                    }
                    settings["LastCheck_Urgent"] = nowTicks;
                }

                // 2. Standard (90 min): Pozostałe aktywne paczki
                var normalParcels = ParcelManager.AllParcels.Where(p => !p.IsArchived && !IsOutForDelivery(p) && !ParcelManager.IsDeliveredStatus(p.Status)).ToList();
                if (normalParcels.Count > 0 && checkNormal)
                {
                    foreach (var p in normalParcels)
                    {
                        string oldStatus = p.Status;
                        // Jeśli to manualna paczka (brak telefonu zalogowanego lub dodana ręcznie) - refresh
                        if (await ParcelManager.RefreshSingleParcel(p))
                        {
                            if (p.Status != oldStatus || force) HandleStatusChange(p, force);
                        }
                    }
                    settings["LastCheck_Normal"] = nowTicks;
                }

                // 3. Discovery (6h): Pełna synchronizacja konta
                {
                    // UpdateAllParcelsAsync robi full sync
                    await ParcelManager.UpdateAllParcelsAsync();
                    // Tutaj notyfikacje o *nowych* paczkach są trudniejsze do wykrycia bez porównania list,
                    // ale UpdateAllParcelsAsync dodaje je na początek listy.
                    // Można by sprawdzić, czy liczba paczek wzrosła, ale na razie skupmy się na zmianach statusu.
                    settings["LastCheck_Discovery"] = nowTicks;
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
            // Sprawdza czy status to "Wydana do doręczenia" (lub z angielska)
            // Używamy helpera z ParcelManager lub prostego stringa
            // "Wydana do doręczenia" status API to zazwyczaj "out_for_delivery"
            return p.OriginalStatus != null && p.OriginalStatus.ToLower().Contains("out_for_delivery");
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
