using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Windows.ApplicationModel.Resources;
using Windows.UI.Xaml;

namespace InPost_Mobile.Models
{
    [DataContract]
    public class ParcelItem
    {
        [DataMember]
        public string TrackingNumber { get; set; }

        [DataMember]
        public string Status { get; set; } 

        [DataMember]
        public string OriginalStatus { get; set; } 

        [DataMember]
        public string Sender { get; set; }
        [DataMember]
        public string Size { get; set; }
        [DataMember]
        public string Icon { get; set; }
        [DataMember]
        public bool IsArchived { get; set; }
        [DataMember]
        public string PickupPointName { get; set; }
        [DataMember]
        public string PickupPointAddress { get; set; }
        [DataMember]
        public string PickupCode { get; set; }
        [DataMember]
        public string LastUpdateDate { get; set; }
        [DataMember]
        public string ParcelType { get; set; }
        [DataMember]
        public List<ParcelEvent> History { get; set; }
        [DataMember]
        public string CustomName { get; set; }

        [DataMember]
        public double Latitude { get; set; }
        [DataMember]
        public double Longitude { get; set; }

        [IgnoreDataMember]
        public Visibility SenderSectionVisibility
        {
            get
            {
                bool hasRealSender = !string.IsNullOrEmpty(Sender) && Sender != "Nadawca";
                return hasRealSender ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        [IgnoreDataMember]
        public string SenderDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(Sender) || Sender == "Nadawca") return "InPost";
                return Sender;
            }
        }

        [IgnoreDataMember]
        public string SenderLabel
        {
            get
            {
                var loader = new ResourceLoader();
                return loader.GetString("LblSender/Text");
            }
        }

        [IgnoreDataMember]
        public Visibility DetailsSenderVisibility
        {
            get
            {
                bool hasRealSender = !string.IsNullOrEmpty(Sender) && Sender != "Nadawca";
                return hasRealSender ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        [IgnoreDataMember]
        public Visibility PickupSectionVisibility
        {
            get
            {
                bool hasPoint = !string.IsNullOrWhiteSpace(PickupPointName);
                return (hasPoint && !IsArchived) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        [IgnoreDataMember]
        public string NameButtonText
        {
            get
            {
                var loader = new ResourceLoader();
                return string.IsNullOrWhiteSpace(CustomName) ? loader.GetString("Btn_NamePlaceholder") : CustomName;
            }
        }

        [IgnoreDataMember]
        public string NameButtonColor
        {
            get { return string.IsNullOrWhiteSpace(CustomName) ? "#AAAAAA" : "#FFFFFF"; }
        }

        [IgnoreDataMember]
        public string SizeName
        {
            get
            {
                if (string.IsNullOrEmpty(Size)) return "Mini";
                var loader = new ResourceLoader();
                switch (Size.ToUpper())
                {
                    case "A": return loader.GetString("Lbl_Size_Small");
                    case "B": return loader.GetString("Lbl_Size_Medium");
                    case "C": return loader.GetString("Lbl_Size_Large");
                    default: return "Mini";
                }
            }
        }
    }
}