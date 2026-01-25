using System;
using System.Collections.Generic;
using System.Linq;

namespace InPost_Mobile.Models
{
    public static class DebugManager
    {
        private static List<ParcelItem> _mockParcels = new List<ParcelItem>();

        public static async System.Threading.Tasks.Task InitializeMockData()
        {
            if (ParcelManager.AllParcels.Count == 0) await ParcelManager.LoadDataAsync();
            if (_mockParcels.Count > 0 || ParcelManager.AllParcels.Count > 0) return;

            var p = new ParcelItem
            {
                TrackingNumber = "123456789012345678901234",
                Status = "Gotowe do odbioru",
                OriginalStatus = "ready_to_pickup",
                Sender = "Debug Sender Store",
                Size = "B",
                PickupPointName = "DEBUG-001",
                PickupPointAddress = "Debug Street 1, Imaginary City",
                PickupCode = "123456",
                ParcelType = "Receive",
                Icon = "\uE8B7",
                IconImage = "ms-appx:///Assets/icon_paczkopunkt.PNG"
            };
            _mockParcels.Add(p);
            ParcelManager.AllParcels.Add(p);
            await ParcelManager.SaveDataAsync();
        }

        public static List<ParcelItem> GetMockParcels(string type)
        {
            return _mockParcels.Where(p => p.ParcelType == type).ToList();
        }

        public static async System.Threading.Tasks.Task AddMockParcel(string tracking, string status, string sender, string type, string code = "---", string target = "Locker", string size = "A", string customName = "")
        {
            if (ParcelManager.AllParcels.Count == 0) await ParcelManager.LoadDataAsync();

            string pointName = "DEBUG-LOCKER";
            string address = "Mock Locker St. 123";
            
            if (target == "Courier")
            {
                pointName = "Kurier";
                address = "Doręczenie kurierem";
            }

            // Determine Icon logic (Mirroring ParcelManager behavior)
            string icon = ParcelManager.GetIconForStatus(status);
            string iconImage = null;

            if (target == "Courier")
            {
                icon = "\uE139";
                iconImage = "ms-appx:///Assets/icon_kurier.png";
            }
            else
            {
                // Locker
                if (type == "Receive" || type == "Send")
                {
                    // Exceptions for Return / Ready
                    string s = status.ToLower();
                    if (s.Contains("return") || s.Contains("zwrócona"))
                    {
                         icon = "\uE117";
                         iconImage = null;
                    }
                    else if (ParcelManager.IsDeliveredStatus(ParcelManager.GetTranslatedStatus(status))) 
                    {
                         icon = "\uE10B";
                         iconImage = null;
                    }
                    else
                    {
                         icon = "\uE8B7";
                         iconImage = "ms-appx:///Assets/icon_paczkopunkt.PNG";
                    }
                }
            }

            var p = new ParcelItem
            {
                TrackingNumber = tracking,
                Status = ParcelManager.GetTranslatedStatus(status), // Try to translate or use raw
                OriginalStatus = status,
                Sender = sender,
                CustomName = customName,
                Size = size,
                PickupPointName = pointName,
                PickupPointAddress = address,
                PickupCode = code,
                ParcelType = type,
                Icon = icon,
                IconImage = iconImage
            };
            _mockParcels.Insert(0, p);
            ParcelManager.AllParcels.Insert(0, p);
            await ParcelManager.SaveDataAsync();
        }
    }
}
