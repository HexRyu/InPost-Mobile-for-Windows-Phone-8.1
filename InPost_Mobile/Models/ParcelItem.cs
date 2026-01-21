using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Windows.ApplicationModel.Resources;
using Windows.UI.Xaml;

namespace InPost_Mobile.Models
{
    [DataContract]
    public class ParcelItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _trackingNumber;
        [DataMember]
        public string TrackingNumber
        {
            get { return _trackingNumber; }
            set { if (_trackingNumber != value) { _trackingNumber = value; OnPropertyChanged(); } }
        }

        private string _status;
        [DataMember]
        public string Status
        {
            get { return _status; }
            set { if (_status != value) { _status = value; OnPropertyChanged(); } }
        }

        private string _originalStatus;
        [DataMember]
        public string OriginalStatus
        {
            get { return _originalStatus; }
            set { if (_originalStatus != value) { _originalStatus = value; OnPropertyChanged(); } }
        }

        private string _sender;
        [DataMember]
        public string Sender
        {
            get { return _sender; }
            set 
            { 
                if (_sender != value) 
                { 
                    _sender = value; 
                    OnPropertyChanged(); 
                    OnPropertyChanged("SenderDisplay");
                    OnPropertyChanged("SenderSectionVisibility");
                    OnPropertyChanged("DetailsSenderVisibility");
                } 
            }
        }

        private string _size;
        [DataMember]
        public string Size
        {
            get { return _size; }
            set 
            { 
                if (_size != value) 
                { 
                    _size = value; 
                    OnPropertyChanged(); 
                    OnPropertyChanged("SizeName");
                } 
            }
        }

        private string _icon;
        [DataMember]
        public string Icon
        {
            get { return _icon; }
            set { if (_icon != value) { _icon = value; OnPropertyChanged(); } }
        }

        private bool _isArchived;
        [DataMember]
        public bool IsArchived
        {
            get { return _isArchived; }
            set 
            { 
                if (_isArchived != value) 
                { 
                    _isArchived = value; 
                    OnPropertyChanged();
                    OnPropertyChanged("PickupSectionVisibility");
                } 
            }
        }

        private string _pickupPointName;
        [DataMember]
        public string PickupPointName
        {
            get { return _pickupPointName; }
            set 
            { 
                if (_pickupPointName != value) 
                { 
                    _pickupPointName = value; 
                    OnPropertyChanged();
                    OnPropertyChanged("PickupSectionVisibility");
                } 
            }
        }

        private string _pickupPointAddress;
        [DataMember]
        public string PickupPointAddress
        {
            get { return _pickupPointAddress; }
            set { if (_pickupPointAddress != value) { _pickupPointAddress = value; OnPropertyChanged(); } }
        }

        private string _pickupCode;
        [DataMember]
        public string PickupCode
        {
            get { return _pickupCode; }
            set { if (_pickupCode != value) { _pickupCode = value; OnPropertyChanged(); } }
        }

        private string _lastUpdateDate;
        [DataMember]
        public string LastUpdateDate
        {
            get { return _lastUpdateDate; }
            set { if (_lastUpdateDate != value) { _lastUpdateDate = value; OnPropertyChanged(); } }
        }

        private string _parcelType;
        [DataMember]
        public string ParcelType
        {
            get { return _parcelType; }
            set { if (_parcelType != value) { _parcelType = value; OnPropertyChanged(); } }
        }

        private List<ParcelEvent> _history;
        [DataMember]
        public List<ParcelEvent> History
        {
            get { return _history; }
            set { if (_history != value) { _history = value; OnPropertyChanged(); } }
        }

        private string _customName;
        [DataMember]
        public string CustomName
        {
            get { return _customName; }
            set 
            { 
                if (_customName != value) 
                { 
                    _customName = value; 
                    OnPropertyChanged(); 
                    OnPropertyChanged("NameButtonText");
                    OnPropertyChanged("NameButtonColor");
                } 
            }
        }

        private double _latitude;
        [DataMember]
        public double Latitude
        {
            get { return _latitude; }
            set { if (_latitude != value) { _latitude = value; OnPropertyChanged(); } }
        }

        private double _longitude;
        [DataMember]
        public double Longitude
        {
            get { return _longitude; }
            set { if (_longitude != value) { _longitude = value; OnPropertyChanged(); } }
        }

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