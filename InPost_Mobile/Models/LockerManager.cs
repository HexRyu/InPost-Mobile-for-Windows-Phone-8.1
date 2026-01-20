using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Web.Http;
using Windows.Web.Http.Filters;
using Windows.Data.Json;
using Windows.ApplicationModel.Resources;
using Windows.UI.Popups;

namespace InPost_Mobile.Models
{
    public static class LockerManager
    {
        private static ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

        private static HttpClient CreateHttpClient()
        {
            var filter = new HttpBaseProtocolFilter();
            filter.IgnorableServerCertificateErrors.Add(Windows.Security.Cryptography.Certificates.ChainValidationResult.Expired);
            filter.IgnorableServerCertificateErrors.Add(Windows.Security.Cryptography.Certificates.ChainValidationResult.Untrusted);
            filter.IgnorableServerCertificateErrors.Add(Windows.Security.Cryptography.Certificates.ChainValidationResult.InvalidName);
            filter.IgnorableServerCertificateErrors.Add(Windows.Security.Cryptography.Certificates.ChainValidationResult.IncompleteChain);

            var client = new HttpClient(filter);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(AppSecrets.UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            return client;
        }

        public static async Task<string> ValidateAndOpenAsync(ParcelItem parcel)
        {
            // 1. Sprawdzenie współrzędnych
            double lat = parcel.Latitude;
            double lon = parcel.Longitude;

            if (lat == 0 || lon == 0)
            {
                throw new Exception("Błąd lokalizacji: Współrzędne paczkomatu wynoszą 0. Odśwież listę paczek na ekranie głównym i spróbuj ponownie.");
            }

            // 2. Przygotowanie numeru telefonu 
            string rawPhone = _localSettings.Values["UserPhone"].ToString().Replace(" ", "").Replace("-", "").Trim();
            string phone = rawPhone;
            if (phone.Length > 9) phone = phone.Substring(phone.Length - 9);

            string token = _localSettings.Values["AuthToken"].ToString();

            // 3. Budowanie JSON
            JsonObject json = new JsonObject();

            JsonObject parcelObj = new JsonObject();
            parcelObj.SetNamedValue("shipmentNumber", JsonValue.CreateStringValue(parcel.TrackingNumber));
            parcelObj.SetNamedValue("openCode", JsonValue.CreateStringValue(parcel.PickupCode));

            JsonObject phoneObj = new JsonObject();
            phoneObj.SetNamedValue("prefix", JsonValue.CreateStringValue("+48"));
            phoneObj.SetNamedValue("value", JsonValue.CreateStringValue(phone));
            parcelObj.SetNamedValue("recieverPhoneNumber", phoneObj);

            JsonObject geoObj = new JsonObject();
            geoObj.SetNamedValue("latitude", JsonValue.CreateNumberValue(lat));
            geoObj.SetNamedValue("longitude", JsonValue.CreateNumberValue(lon));
            geoObj.SetNamedValue("accuracy", JsonValue.CreateNumberValue(13.365));

            json.SetNamedValue("parcel", parcelObj);
            json.SetNamedValue("geoPoint", geoObj);

            using (var client = CreateHttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new Windows.Web.Http.Headers.HttpCredentialsHeaderValue("Bearer", token);

                // A. VALIDATE
                var validateResp = await client.PostAsync(
                    new Uri(AppSecrets.BaseUrl + "v2/collect/validate"),
                    new HttpStringContent(json.ToString(), Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json"));

                string valResponseStr = await validateResp.Content.ReadAsStringAsync();

                if (!validateResp.IsSuccessStatusCode)
                {
                    // Zwracamy dokładny błąd z API
                    throw new Exception($"Błąd walidacji ({validateResp.StatusCode}): {valResponseStr}");
                }

                JsonObject valObj = JsonObject.Parse(valResponseStr);
                string sessionUuid = "";
                if (valObj.ContainsKey("sessionUuid")) sessionUuid = valObj.GetNamedString("sessionUuid");
                else throw new Exception("Brak sessionUuid w odpowiedzi.");

                // B. OPEN
                JsonObject openJson = new JsonObject();
                openJson.SetNamedValue("sessionUuid", JsonValue.CreateStringValue(sessionUuid));

                var openResp = await client.PostAsync(
                    new Uri(AppSecrets.BaseUrl + "v1/collect/compartment/open"),
                    new HttpStringContent(openJson.ToString(), Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json"));

                if (!openResp.IsSuccessStatusCode)
                {
                    string openErr = await openResp.Content.ReadAsStringAsync();
                    throw new Exception($"Błąd otwarcia ({openResp.StatusCode}): {openErr}");
                }

                return sessionUuid;
            }
        }

        public static async Task<bool> IsLockerClosedAsync(string sessionUuid)
        {
            try
            {
                string token = _localSettings.Values["AuthToken"].ToString();
                JsonObject json = new JsonObject();
                json.SetNamedValue("sessionUuid", JsonValue.CreateStringValue(sessionUuid));
                json.SetNamedValue("expectedStatus", JsonValue.CreateStringValue("CLOSED"));

                using (var client = CreateHttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new Windows.Web.Http.Headers.HttpCredentialsHeaderValue("Bearer", token);
                    var response = await client.PostAsync(
                        new Uri(AppSecrets.BaseUrl + "v1/collect/compartment/status"),
                        new HttpStringContent(json.ToString(), Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json"));

                    return response.IsSuccessStatusCode;
                }
            }
            catch { }
            return false;
        }

        public static async Task TerminateSessionAsync(string sessionUuid)
        {
            try
            {
                string token = _localSettings.Values["AuthToken"].ToString();
                JsonObject json = new JsonObject();
                json.SetNamedValue("sessionUuid", JsonValue.CreateStringValue(sessionUuid));

                using (var client = CreateHttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new Windows.Web.Http.Headers.HttpCredentialsHeaderValue("Bearer", token);
                    await client.PostAsync(
                        new Uri(AppSecrets.BaseUrl + "v1/collect/compartment/terminate"),
                        new HttpStringContent(json.ToString(), Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json"));
                }
            }
            catch { }
        }
    }
}