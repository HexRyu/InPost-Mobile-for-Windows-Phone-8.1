using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using InPost_Mobile.Models;

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
        }

        private void AddMock_Click(object sender, RoutedEventArgs e)
        {
            string tracking = TbTracking.Text.Trim();
            if (string.IsNullOrWhiteSpace(tracking) || tracking.Length < 10)
            {
                TxtStatus.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Red);
                TxtStatus.Text = "Invalid Tracking Number";
                return;
            }

            string senderName = TbSender.Text.Trim();
            if (string.IsNullOrEmpty(senderName)) senderName = "Debug Sender";

            string status = (CbStatus.SelectedItem as ComboBoxItem)?.Tag.ToString();
            string code = TbCode.Text.Trim();

            string type = "Receive";
            if (RbSend.IsChecked == true) type = "Send";
            if (RbReturn.IsChecked == true) type = "Return";

            string target = "Locker";
            if (RbCourier.IsChecked == true) target = "Courier";

            DebugManager.AddMockParcel(tracking, status, senderName, type, code, target);

            TxtStatus.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Green);
            TxtStatus.Text = "Parcel Added!";
        }
    }
}
