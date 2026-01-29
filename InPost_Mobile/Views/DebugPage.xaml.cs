using System; // Restored
using InPost_Mobile.Models; // Ensure Models is included
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using InPost_Mobile.Tasks;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;
using Windows.ApplicationModel.Resources;

namespace InPost_Mobile.Views
{
    public sealed partial class DebugPage : Page
    {
        public DebugPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            PopulateStatuses();
            PopulateDelays();
            LoadCurrentPhone();
        }

        private void LoadCurrentPhone()
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSettings.Values.ContainsKey("UserPhone"))
            {
                string phone = localSettings.Values["UserPhone"]?.ToString() ?? "";
                TbPhone.Text = phone;
                TxtPhoneStatus.Text = $"Current: {phone}";
                TxtPhoneStatus.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.LightGreen);
            }
            else
            {
                TxtPhoneStatus.Text = "No phone set - QR will use fallback";
                TxtPhoneStatus.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Gray);
            }
        }

        private void RandomPhone_Click(object sender, RoutedEventArgs e)
        {
            var random = new Random();
            string phone = $"{random.Next(100, 999)} {random.Next(100, 999)} {random.Next(100, 999)}";
            TbPhone.Text = phone;
            TxtPhoneStatus.Text = "Random generated - click Save to apply";
            TxtPhoneStatus.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Orange);
        }

        private void SavePhone_Click(object sender, RoutedEventArgs e)
        {
            string phone = TbPhone.Text.Trim();
            
            if (string.IsNullOrEmpty(phone))
            {
                TxtPhoneStatus.Text = "Phone cleared!";
                TxtPhoneStatus.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Red);
                Windows.Storage.ApplicationData.Current.LocalSettings.Values.Remove("UserPhone");
                return;
            }

            // Clean and validate
            phone = phone.Replace(" ", "").Replace("-", "");
            
            if (phone.Length < 9)
            {
                TxtPhoneStatus.Text = "Phone too short (min 9 digits)";
                TxtPhoneStatus.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Red);
                return;
            }

            // Save to LocalSettings
            Windows.Storage.ApplicationData.Current.LocalSettings.Values["UserPhone"] = phone;
            
            TxtPhoneStatus.Text = $"✓ Saved: {phone}";
            TxtPhoneStatus.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.LightGreen);
        }

        private void PopulateDelays()
        {
            CbNotifDelay.Items.Clear();
            CbNotifDelay.Items.Add("No Notification");
            CbNotifDelay.Items.Add("30 seconds");
            for (int i = 1; i <= 30; i++)
            {
                string label = i == 1 ? "1 minute" : $"{i} minutes";
                CbNotifDelay.Items.Add(label);
            }
            CbNotifDelay.SelectedIndex = 0;
        }

        private void PopulateStatuses()
        {
            if (CbStatus == null) return;

            var statuses = new List<string>
            {
                "created", "confirmed", "collected_from_sender",
                "adopted_at_source_branch", "sent_from_source_branch",
                "adopted_at_sorting_center", "sent_from_sorting_center",
                "adopted_at_target_branch", "out_for_delivery",
                "ready_to_pickup", "pickup_reminder_set", "delivered",
                "not_found", "avizo", "returned_to_sender",
                "Zwrot_nadany", "taken_by_courier_from_pok", "dispatched_by_sender_to_pok",
                "return_pickup_confirmation", "stack_in_box_machine",
                "stack_in_customer_service_point"
            };

            CbStatus.Items.Clear();
            foreach (var s in statuses)
            {
                string translated = ParcelManager.GetTranslatedStatus(s);
                ComboBoxItem item = new ComboBoxItem();
                item.Content = translated + " (" + s + ")"; // Show translated + raw code for clarity
                item.Tag = s;
                CbStatus.Items.Add(item);
            }
            CbStatus.SelectedIndex = 9; // Default to ready_to_pickup
            CheckDelayVisibility();
        }

        private void CbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CheckDelayVisibility();
        }

        private void CheckDelayVisibility()
        {
            if (CbStatus == null || CbNotifDelay == null) return;
            string tag = (CbStatus.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
            
            bool show = tag.Contains("out_for_delivery") || tag.Contains("ready_to_pickup");
            CbNotifDelay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void AddMock_Click(object sender, RoutedEventArgs e)
        {
            string tracking = TbTracking.Text.Trim();
            if (string.IsNullOrWhiteSpace(tracking) || tracking.Length < 10)
            {
                TxtStatus.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Red);
                TxtStatus.Text = "Invalid Tracking Number";
                return;
            }

            string senderName = TbSender.Text.Trim();
            // User requested empty sender if not provided
            
            string customName = TbCustomName.Text.Trim();

            string status = (CbStatus.SelectedItem as ComboBoxItem)?.Tag.ToString();
            string code = TbCode.Text.Trim();

            string type = "Receive";
            if (RbReceive.IsChecked == true) type = "Receive"; // Fix IsChecked check order if needed, but Receive is default
            if (RbSend.IsChecked == true) type = "Send";
            if (RbReturn.IsChecked == true) type = "Return";

            string target = "Locker";
            if (RbCourier.IsChecked == true) target = "Courier";

            string size = (CbSize.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "A";

            await DebugManager.AddMockParcel(tracking, status, senderName, type, code, target, size, customName);
            TileManager.Update(ParcelManager.AllParcels); // Update tile immediately

            TxtStatus.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Green);
            TxtStatus.Text = "Parcel Added!";

            int delayIndex = CbNotifDelay.SelectedIndex;

            if (CbNotifDelay.Visibility == Visibility.Visible && delayIndex > 0)
            {
                if (delayIndex == 1) // 30 seconds
                {
                    ScheduleToast(tracking, senderName, status, 0.5, customName); 
                    TxtStatus.Text += " + Scheduled in 30s!";
                }
                else // Scheduled minutes
                {
                    int minutes = delayIndex - 1;
                    ScheduleToast(tracking, senderName, status, minutes, customName);
                    TxtStatus.Text += $" + Scheduled in {minutes} min!";
                }
            }
        }

        private void ScheduleToast(string tracking, string sender, string status, double minutes, string customName = "")
        {
             var loader = new ResourceLoader();
             string message = "";
             string title = "InPost Mobile";
             
             // Same logic as BackgroundSyncTask/Live Tile:
             // 1. Custom Name
             // 2. Sender (if not "Nadawca" and not empty)
             // 3. Tracking Number
             
             string displayName = !string.IsNullOrEmpty(customName) ? customName : sender;
             if (string.IsNullOrEmpty(displayName) || displayName == "Nadawca") displayName = tracking;

             bool isOut = status.Contains("out_for_delivery");

             if (isOut)
             {
                 if (!string.IsNullOrEmpty(customName)) message = string.Format(loader.GetString("Notif_Out_Name"), customName);
                 else if (!string.IsNullOrEmpty(sender) && sender != "Nadawca") message = string.Format(loader.GetString("Notif_Out_Sender"), sender);
                 else message = string.Format(loader.GetString("Notif_Out_Number"), tracking);
             }
             else // Ready
             {
                 if (!string.IsNullOrEmpty(customName)) message = string.Format(loader.GetString("Notif_Ready_Name"), customName);
                 else if (!string.IsNullOrEmpty(sender) && sender != "Nadawca") message = string.Format(loader.GetString("Notif_Ready_Sender"), sender);
                 else message = string.Format(loader.GetString("Notif_Ready_Number"), tracking);
             }

             var template = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText04);
             var textNodes = template.GetElementsByTagName("text");
             textNodes[0].InnerText = title;
             textNodes[1].InnerText = message;

             var toastNav = template.CreateAttribute("launch");
             toastNav.Value = tracking;
             template.SelectSingleNode("/toast").Attributes.SetNamedItem(toastNav);

             // Add Audio
             var audio = template.CreateElement("audio");
             audio.SetAttribute("src", "ms-winsoundevent:Notification.Default");
             template.SelectSingleNode("/toast").AppendChild(audio);

             var scheduledToast = new ScheduledToastNotification(template, DateTime.Now.AddMinutes(minutes));
             ToastNotificationManager.CreateToastNotifier().AddToSchedule(scheduledToast);
        }
    }
}
