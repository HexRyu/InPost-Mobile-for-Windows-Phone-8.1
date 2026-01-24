using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Web.Http;
using Windows.Web.Http.Filters;
using Windows.Web.Http.Headers;
using Windows.Security.Cryptography.Certificates;
using Windows.Data.Json;
using Windows.ApplicationModel.Resources;
using Windows.UI.Popups;
using System.Text;
using System.Globalization;
using Windows.Storage.Streams;

namespace InPost_Mobile.Models
{
    public static class ParcelManager
    {
        public static List<ParcelItem> AllParcels = new List<ParcelItem>();
        private const string DATA_FILENAME = "parcels_data.json";
        private static ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;
        private static ResourceLoader _loader = new ResourceLoader();
        public static bool IsDebugMode { get; set; } = false;

        private static HttpClient CreateHttpClient()
        {
            var filter = new HttpBaseProtocolFilter();
            filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.Expired);
            filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.Untrusted);
            filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.InvalidName);
            filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.IncompleteChain);

            var client = new HttpClient(filter);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(AppSecrets.UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            return client;
        }

        public static bool IsLoggedIn() => _localSettings.Values.ContainsKey("AuthToken");

        public static async void Logout()
        {
            _localSettings.Values.Remove("AuthToken");
            _localSettings.Values.Remove("RefreshToken");
            _localSettings.Values.Remove("UserPhone");
            foreach (var p in AllParcels)
            {
                p.PickupCode = "";
                p.PickupPointAddress = _loader.GetString("Txt_CheckSms");
            }
            await SaveDataAsync();
        }

        // --- ZMIANA: PUBLIC (żeby LockerManager mógł tego użyć) ---
        public static async Task<bool> RefreshAuthTokenAsync()
        {
            if (!_localSettings.Values.ContainsKey("RefreshToken")) return false;

            try
            {
                string refreshToken = _localSettings.Values["RefreshToken"].ToString();
                JsonObject json = new JsonObject();
                json.SetNamedValue("refreshToken", JsonValue.CreateStringValue(refreshToken));

                using (var client = CreateHttpClient())
                {
                    var content = new HttpStringContent(json.ToString(), Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json");
                    var response = await client.PostAsync(new Uri(AppSecrets.BaseUrl + "v1/account/token/refresh"), content);

                    if (response.IsSuccessStatusCode)
                    {
                        string respStr = await response.Content.ReadAsStringAsync();
                        JsonObject obj = JsonObject.Parse(respStr);
                        if (obj.ContainsKey("authToken"))
                        {
                            _localSettings.Values["AuthToken"] = obj.GetNamedString("authToken");
                            if (obj.ContainsKey("refreshToken"))
                                _localSettings.Values["RefreshToken"] = obj.GetNamedString("refreshToken");
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        // ... RESZTA KODU BEZ ZMIAN (UpdateAllParcelsAsync itd.) ...
        public static async Task UpdateAllParcelsAsync()
        {
            bool wasUpdated = false;
            if (IsLoggedIn())
            {
                bool r1 = await DownloadFromEndpoint(AppSecrets.BaseUrl + "v4/parcels/tracked", "Receive");
                bool r2 = await DownloadFromEndpoint(AppSecrets.BaseUrl + "v4/parcels/sent", "Send");
                bool r3 = await DownloadFromEndpoint(AppSecrets.BaseUrl + "v1/returns/tickets", "Return");
                if (r1 || r2 || r3) wasUpdated = true;
            }
            var manualParcels = AllParcels.Where(p => !p.IsArchived && !IsDeliveredStatus(p.Status) && string.IsNullOrEmpty(p.PickupCode)).ToList();
            foreach (var parcel in manualParcels)
            {
                if (await RefreshSingleParcel(parcel)) wasUpdated = true;
            }
            if (wasUpdated) await SaveDataAsync();
        }

        public static bool IsDeliveredStatus(string status)
        {
            if (string.IsNullOrEmpty(status)) return false;
            string s = status.ToLower();
            return s == _loader.GetString("Status_delivered").ToLower() ||
                   s == _loader.GetString("Status_picked_up").ToLower() ||
                   s == _loader.GetString("Status_returned_to_sender").ToLower() ||
                   s.Contains("dostarczona") || s.Contains("odebrana") || s.Contains("canceled") || s.Contains("anulowana");
        }

        private static async Task<bool> DownloadFromEndpoint(string url, string parcelType)
        {
            try
            {
                if (!_localSettings.Values.ContainsKey("AuthToken")) return false;
                string token = _localSettings.Values["AuthToken"].ToString();

                using (var client = CreateHttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new Windows.Web.Http.Headers.HttpCredentialsHeaderValue("Bearer", token);
                    var response = await client.GetAsync(new Uri(url));

                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        bool refreshed = await RefreshAuthTokenAsync();
                        if (refreshed)
                        {
                            token = _localSettings.Values["AuthToken"].ToString();
                            client.DefaultRequestHeaders.Authorization = new Windows.Web.Http.Headers.HttpCredentialsHeaderValue("Bearer", token);
                            response = await client.GetAsync(new Uri(url));
                        }
                        else return false;
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        IBuffer buffer = await response.Content.ReadAsBufferAsync();
                        string jsonString;
                        using (var dataReader = DataReader.FromBuffer(buffer)) { dataReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8; jsonString = dataReader.ReadString(buffer.Length); }
                        JsonObject json = JsonObject.Parse(jsonString);
                        ParseAndAddParcels(json, parcelType);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static void ParseAndAddParcels(JsonObject json, string enforcedType)
        {
            JsonArray items = null;
            if (json.ContainsKey("parcels")) items = json.GetNamedArray("parcels");
            else if (json.ContainsKey("items")) items = json.GetNamedArray("items");
            else if (json.ContainsKey("tickets")) items = json.GetNamedArray("tickets");

            if (items != null)
            {
                foreach (var item in items)
                {
                    try
                    {
                        var obj = item.GetObject();
                        string tracking = "";
                        if (obj.ContainsKey("shipmentNumber")) tracking = obj.GetNamedString("shipmentNumber");
                        else if (obj.ContainsKey("tracking_number")) tracking = obj.GetNamedString("tracking_number");

                        if (string.IsNullOrEmpty(tracking)) continue;

                        var existing = AllParcels.FirstOrDefault(p => p.TrackingNumber == tracking);
                        if (existing == null)
                        {
                            var newParcel = new ParcelItem { TrackingNumber = tracking, IsArchived = false, History = new List<ParcelEvent>(), ParcelType = enforcedType };
                            ParseApiObjectToParcel(newParcel, obj);
                            AllParcels.Insert(0, newParcel);
                        }
                        else
                        {
                            existing.ParcelType = enforcedType;
                            ParseApiObjectToParcel(existing, obj);
                        }
                    }
                    catch { }
                }
            }
        }

        private static void ParseApiObjectToParcel(ParcelItem parcel, JsonObject json)
        {
            string apiStatus = "unknown";
            if (json.ContainsKey("status")) apiStatus = json.GetNamedString("status");
            parcel.OriginalStatus = apiStatus;
            parcel.Status = GetTranslatedStatus(apiStatus);

            if (string.IsNullOrEmpty(parcel.Sender) || parcel.Sender == "Nadawca")
            {
                parcel.Sender = "Nadawca";
                if (json.ContainsKey("sender")) { try { var s = json.GetNamedObject("sender"); if (s.ContainsKey("name")) parcel.Sender = s.GetNamedString("name"); } catch { } }
            }

            if (json.ContainsKey("openCode")) parcel.PickupCode = json.GetNamedString("openCode");

            if (json.ContainsKey("parcelSize"))
            {
                parcel.Size = json.GetNamedString("parcelSize");
            }

            bool isCourier = false;
            string newPointName = "";
            string newPointAddress = "";
            double lat = 0;
            double lon = 0;

            string shipmentType = json.ContainsKey("shipmentType") ? json.GetNamedString("shipmentType").ToLower() : "";
            if (shipmentType.Contains("courier")) isCourier = true;

            JsonObject pickupObj = null;
            if (json.ContainsKey("pickUpPoint") && json["pickUpPoint"].ValueType == JsonValueType.Object) pickupObj = json.GetNamedObject("pickUpPoint");
            else if (json.ContainsKey("pickup_point") && json["pickup_point"].ValueType == JsonValueType.Object) pickupObj = json.GetNamedObject("pickup_point");

            if (pickupObj != null)
            {
                if (pickupObj.ContainsKey("name")) newPointName = pickupObj.GetNamedString("name");
                if (pickupObj.ContainsKey("addressDetails"))
                {
                    try
                    {
                        var addr = pickupObj.GetNamedObject("addressDetails");
                        string street = addr.ContainsKey("street") ? addr.GetNamedString("street") : "";
                        string bNo = addr.ContainsKey("buildingNo") ? addr.GetNamedString("buildingNo") : "";
                        string city = addr.ContainsKey("city") ? addr.GetNamedString("city") : "";
                        string zip = addr.ContainsKey("postCode") ? addr.GetNamedString("postCode") : "";
                        if (!string.IsNullOrEmpty(street)) newPointAddress = $"{street} {bNo}\n{zip} {city}";
                    }
                    catch { }
                }
                if (string.IsNullOrEmpty(newPointAddress) && pickupObj.ContainsKey("locationDescription")) newPointAddress = pickupObj.GetNamedString("locationDescription");

                if (pickupObj.ContainsKey("location"))
                {
                    try
                    {
                        var loc = pickupObj.GetNamedObject("location");
                        if (loc.ContainsKey("latitude")) lat = loc.GetNamedNumber("latitude");
                        if (loc.ContainsKey("longitude")) lon = loc.GetNamedNumber("longitude");
                    }
                    catch { }
                }
            }

            if (string.IsNullOrEmpty(newPointName) && json.ContainsKey("custom_attributes"))
            {
                try { var ca = json.GetNamedObject("custom_attributes"); if (ca.ContainsKey("target_machine_id")) newPointName = ca.GetNamedString("target_machine_id"); } catch { }
            }

            if (isCourier)
            {
                parcel.PickupPointName = "Kurier";
                parcel.PickupPointAddress = _loader.GetString("Txt_CourierDelivery");
            }
            else
            {
                if (!string.IsNullOrEmpty(newPointName)) parcel.PickupPointName = newPointName;
                else if (string.IsNullOrEmpty(parcel.PickupPointName)) parcel.PickupPointName = "Paczkomat";

                if (!string.IsNullOrWhiteSpace(newPointAddress)) parcel.PickupPointAddress = newPointAddress.Trim();
                else if (string.IsNullOrWhiteSpace(parcel.PickupPointAddress) || parcel.PickupPointAddress == _loader.GetString("Txt_CheckSms")) parcel.PickupPointAddress = _loader.GetString("Txt_CheckSms");

                if (lat != 0 && lon != 0)
                {
                    parcel.Latitude = lat;
                    parcel.Longitude = lon;
                }
            }

            if (isCourier) parcel.Icon = "\uE139"; else parcel.Icon = "\uE18B";
            if (IsDeliveredStatus(parcel.Status)) parcel.Icon = "\uE10B";
            else if (apiStatus.ToLower().Contains("return") || apiStatus.ToLower().Contains("zwrócona")) parcel.Icon = "\uE19F";
            else if (apiStatus.ToLower().Contains("canceled") || apiStatus.ToLower().Contains("anulowana")) parcel.Icon = "\uE10A";

            if (IsDeliveredStatus(parcel.Status) || apiStatus.ToLower() == "canceled")
            {
                parcel.PickupPointName = ""; parcel.PickupPointAddress = ""; parcel.PickupCode = "";
            }

            var tempList = new List<TempEvent>();
            if (json.ContainsKey("events"))
            {
                var eventsArr = json.GetNamedArray("events");
                foreach (var item in eventsArr)
                {
                    var evObj = item.GetObject();
                    string name = evObj.ContainsKey("eventTitle") ? evObj.GetNamedString("eventTitle") : "Status";
                    string dateStr = evObj.ContainsKey("date") ? evObj.GetNamedString("date") : DateTime.Now.ToString("o");
                    DateTimeOffset realDate; if (!DateTimeOffset.TryParse(dateStr, out realDate)) realDate = DateTimeOffset.MinValue;
                    tempList.Add(new TempEvent { Status = name, RealDate = realDate });
                }
            }
            else if (json.ContainsKey("tracking_details"))
            {
                var trackingArr = json.GetNamedArray("tracking_details");
                foreach (var item in trackingArr)
                {
                    var evObj = item.GetObject();
                    string status = evObj.GetNamedString("status", "unknown");
                    string dateStr = evObj.GetNamedString("datetime", DateTime.Now.ToString("o"));
                    DateTimeOffset realDate; if (!DateTimeOffset.TryParse(dateStr, out realDate)) realDate = DateTimeOffset.MinValue;
                    tempList.Add(new TempEvent { Status = status, RealDate = realDate });
                }
            }

            if (tempList.Count > 0)
            {
                var sortedList = tempList.OrderByDescending(x => x.RealDate).ToList();
                var finalHistory = new List<ParcelEvent>();
                foreach (var item in sortedList)
                {
                    string originalStatusName = item.Status;
                    string translatedDesc = GetTranslatedStatus(originalStatusName);
                    finalHistory.Add(new ParcelEvent { 
                        Description = translatedDesc, 
                        OriginalStatus = originalStatusName,
                        Date = item.RealDate.ToString("dd.MM HH:mm"), 
                        Color = GetColorForStatus(originalStatusName), 
                        Opacity = (finalHistory.Count == 0) ? 1.0 : 0.6, 
                        IsFirst = false 
                    });
                }
                if (finalHistory.Count > 0) { finalHistory[0].IsFirst = true; parcel.LastUpdateDate = finalHistory[0].Date; parcel.Status = finalHistory[0].Description; }
                parcel.History = finalHistory;
            }
        }

        private class TempEvent { public string Status; public DateTimeOffset RealDate; }

        public static string GetTranslatedStatus(string apiStatus)
        {
            if (string.IsNullOrEmpty(apiStatus)) return "";
            string cleanStatus = apiStatus.Replace(" ", "_").Replace("/", "_").Replace("(", "").Replace(")", "").Trim();
            string key = "Status_" + cleanStatus;
            string translated = _loader.GetString(key);
            if (!string.IsNullOrEmpty(translated)) return translated;
            return apiStatus;
        }

        public static async Task ReloadAllParcelsTranslation()
        {
            _loader = new ResourceLoader();
            foreach (var parcel in AllParcels)
            {
                if (!string.IsNullOrEmpty(parcel.OriginalStatus))
                    parcel.Status = GetTranslatedStatus(parcel.OriginalStatus);

                if (parcel.History != null)
                {
                    foreach (var ev in parcel.History)
                        if (!string.IsNullOrEmpty(ev.OriginalStatus))
                            ev.Description = GetTranslatedStatus(ev.OriginalStatus);
                    if (parcel.History.Count > 0)
                        parcel.Status = parcel.History[0].Description;
                }
                if (parcel.PickupPointName == "Kurier" || parcel.PickupPointAddress == "Doręczenie kurierem" || parcel.PickupPointAddress == "Courier delivery")
                    parcel.PickupPointAddress = _loader.GetString("Txt_CourierDelivery");
            }
            await SaveDataAsync();
        }

        public static string GetIconForStatus(string s)
        {
            s = s.ToLower(); if (s.Contains("delivered") || s.Contains("odebrana") || s.Contains("dostarczona")) return "\uE10B";
            if (s.Contains("return") || s.Contains("zwrócona")) return "\uE19F"; if (s.Contains("canceled") || s.Contains("anulowana")) return "\uE10A"; return "\uE18B";
        }
        public static string GetColorForStatus(string s)
        {
            s = s.ToLower(); if (s.Contains("delivered") || s.Contains("odebrana") || s.Contains("dostarczona")) return "#00CC00";
            if (s.Contains("return") || s.Contains("zwrócona") || s.Contains("canceled") || s.Contains("anulowana")) return "#FF4444";
            if (string.IsNullOrWhiteSpace(s) || s.Contains("unknown")) return "#AAAAAA"; return "#FFCC00";
        }

        public static async Task<bool> AddParcelFromApi(string trackingNumber, string pickupCode = "---")
        {
            if (string.IsNullOrWhiteSpace(trackingNumber) || trackingNumber.Length < 10) return false;
            if (AllParcels.Any(p => p.TrackingNumber == trackingNumber)) { await UpdateSingleParcelAsync(trackingNumber); return true; }
            if (string.IsNullOrWhiteSpace(pickupCode)) pickupCode = "---";
            try
            {
                using (var client = CreateHttpClient())
                {
                    string url = $"{AppSecrets.ShipXUrl}v1/tracking/{trackingNumber}";
                    var response = await client.GetAsync(new Uri(url));
                    if (!response.IsSuccessStatusCode) return false;
                    IBuffer buffer = await response.Content.ReadAsBufferAsync(); string jsonString;
                    using (var dataReader = DataReader.FromBuffer(buffer)) { dataReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8; jsonString = dataReader.ReadString(buffer.Length); }
                    JsonObject json = JsonObject.Parse(jsonString);
                    var newParcel = new ParcelItem { TrackingNumber = trackingNumber, IsArchived = false, PickupCode = pickupCode, History = new List<ParcelEvent>(), ParcelType = "Receive" };
                    ParseApiObjectToParcel(newParcel, json); AllParcels.Insert(0, newParcel); await SaveDataAsync(); return true;
                }
            }
            catch { return false; }
        }
        public static async Task UpdateSingleParcelAsync(string trackingNumber)
        {
            var parcel = AllParcels.FirstOrDefault(p => p.TrackingNumber == trackingNumber);
            if (parcel != null && await RefreshSingleParcel(parcel)) await SaveDataAsync();
        }
        private static async Task<bool> RefreshSingleParcel(ParcelItem parcel)
        {
            try
            {
                using (var client = CreateHttpClient())
                {
                    string url = $"{AppSecrets.ShipXUrl}v1/tracking/{parcel.TrackingNumber}";
                    var response = await client.GetAsync(new Uri(url));
                    if (response.IsSuccessStatusCode)
                    {
                        IBuffer buffer = await response.Content.ReadAsBufferAsync(); string jsonString;
                        using (var dataReader = DataReader.FromBuffer(buffer)) { dataReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8; jsonString = dataReader.ReadString(buffer.Length); }
                        JsonObject json = JsonObject.Parse(jsonString); ParseApiObjectToParcel(parcel, json); return true;
                    }
                }
            }
            catch { }
            return false;
        }
        public static async Task<bool> RequestSmsCode(string p) { try { using (var c = CreateHttpClient()) { string j = "{\"phoneNumber\":{\"prefix\":\"+48\",\"value\":\"" + p + "\"}}"; var ct = new HttpStringContent(j, Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json"); var r = await c.PostAsync(new Uri(AppSecrets.BaseUrl + "v1/account"), ct); return r.IsSuccessStatusCode; } } catch { return false; } }
        public static async Task<bool> VerifySmsCode(string p, string s) { try { using (var c = CreateHttpClient()) { string j = "{\"phoneNumber\":{\"prefix\":\"+48\",\"value\":\"" + p + "\"},\"smsCode\":\"" + s + "\",\"devicePlatform\":\"Android\"}"; var ct = new HttpStringContent(j, Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json"); var r = await c.PostAsync(new Uri(AppSecrets.BaseUrl + "v1/account/verification"), ct); if (r.IsSuccessStatusCode) { string t = await r.Content.ReadAsStringAsync(); JsonObject o = JsonObject.Parse(t); if (o.ContainsKey("authToken")) { _localSettings.Values["AuthToken"] = o.GetNamedString("authToken"); if (o.ContainsKey("refreshToken")) _localSettings.Values["RefreshToken"] = o.GetNamedString("refreshToken"); _localSettings.Values["UserPhone"] = "+48 " + p; return true; } } return false; } } catch { return false; } }
        public static async Task LoadDataAsync() { try { StorageFile f = await ApplicationData.Current.LocalFolder.GetFileAsync(DATA_FILENAME); using (var s = await f.OpenStreamForReadAsync()) { var ser = new DataContractJsonSerializer(typeof(List<ParcelItem>)); AllParcels = (List<ParcelItem>)ser.ReadObject(s); } } catch { AllParcels = new List<ParcelItem>(); } }
        public static async Task SaveDataAsync() 
        { 
            if (IsDebugMode) return;
            try { StorageFile f = await ApplicationData.Current.LocalFolder.CreateFileAsync(DATA_FILENAME, CreationCollisionOption.ReplaceExisting); using (var s = await f.OpenStreamForWriteAsync()) { var ser = new DataContractJsonSerializer(typeof(List<ParcelItem>)); ser.WriteObject(s, AllParcels); } } catch { } 
        }
        public static async Task ForceSave() { await SaveDataAsync(); }
        public static async void RemoveParcel(ParcelItem p) { if (AllParcels.Contains(p)) { AllParcels.Remove(p); await SaveDataAsync(); } }
        public static async Task RenameParcel(string t, string n) { var p = AllParcels.FirstOrDefault(x => x.TrackingNumber == t); if (p != null) { p.CustomName = n; await SaveDataAsync(); } }
        public static void InitializeData() { }
        public static ObservableCollection<ParcelItem> GetActiveParcels(string t) => new ObservableCollection<ParcelItem>(AllParcels.Where(p => !p.IsArchived && p.ParcelType == t).ToList());
        public static ObservableCollection<ParcelItem> GetArchivedParcels(string t) => new ObservableCollection<ParcelItem>(AllParcels.Where(p => p.IsArchived && p.ParcelType == t).ToList());
        public static string GetParcelSectionName(string status)
        {
             if (string.IsNullOrEmpty(status)) return "Other";
             string s = status.ToLower();
             if (s == _loader.GetString("Status_ready_to_pickup").ToLower() || s.Contains("pickup_ready")) return _loader.GetString("Section_ReadyForPickup"); 
             if (s == _loader.GetString("Status_out_for_delivery").ToLower() || s.Contains("out_for_delivery")) return _loader.GetString("Section_OutForDelivery");
             if (s == _loader.GetString("Status_delivered").ToLower() || s == _loader.GetString("Status_picked_up").ToLower() || s.Contains("delivered")) return _loader.GetString("Section_Delivered");
             if (s.Contains("in_transit") || s.Contains("sent") || s.Contains("prepared")) return _loader.GetString("Section_InTransit");
             return _loader.GetString("Section_InTransit"); 
        }
    }
}