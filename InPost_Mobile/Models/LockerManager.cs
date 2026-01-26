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
            // Zmiana User-Agent na zgodny z działającym kodem 3DS
            client.DefaultRequestHeaders.UserAgent.ParseAdd("InPost-Mobile/3.46.0(34600200) (Horizon 11.17.0-50U; AW715988204; Nintendo 3DS; pl)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            return client;
        }

        public static async Task<string> ValidateAndOpenAsync(ParcelItem parcel)
        {
            if (parcel == null) throw new Exception("Błąd wewnętrzny: Pusty obiekt paczki.");

            if (ParcelManager.IsDebugMode)
            {
                await Task.Delay(2000); // Simulate network
                return "debug-session-uuid";
            }

            // 1. Sprawdzenie współrzędnych
            double lat = parcel.Latitude;
            double lon = parcel.Longitude;

            if (lat == 0 || lon == 0)
            {
                throw new Exception("Błąd lokalizacji: Współrzędne paczkomatu wynoszą 0. Odśwież listę paczek na ekranie głównym i spróbuj ponownie.");
            }

            // 2. Przygotowanie numeru telefonu 
            string rawPhone = "";
            
            // PRIORITY: Use phone number from the parcel itself (as per inpost_api.c)
            if (!string.IsNullOrEmpty(parcel.PhoneNumber))
            {
                rawPhone = parcel.PhoneNumber;
            }
            // FALLBACK: Use global setting
            else if (_localSettings.Values.ContainsKey("UserPhone"))
            {
                rawPhone = _localSettings.Values["UserPhone"]?.ToString() ?? "";
            }
            
            if (string.IsNullOrEmpty(rawPhone))
            {
                 // Fallback - spróbuj pobrać z tokena lub wymuś ponowne logowanie?
                 throw new Exception("Błąd: Brak numeru telefonu przypisanego do paczki lub konta.");
            }

            rawPhone = rawPhone.Replace(" ", "").Replace("-", "").Trim();
            string phone = rawPhone;
            if (phone.Length > 9) phone = phone.Substring(phone.Length - 9);

             string token = _localSettings.Values["AuthToken"].ToString();

            // --- STRATEGIA: Metoda 1 (V2), jak błąd to Metoda 2 (V1) ---
            
            try
            {
                // METODA 1: Standardowa (v2/collect/validate -> v1 open)
                return await OpenMethod_ValidateV2(parcel, lat, lon, phone, token, retryCount: 1);
            }
            catch (Exception ex1)
            {
                // METODA 2: Fallback (v1/collect/validate -> v1 open)
                try
                {
                    return await OpenMethod_ValidateV1(parcel, lat, lon, phone, token);
                }
                catch (Exception ex2)
                {
                    // Jeśli obie zawiodą, pokazujemy błędy obu
                    throw new Exception($"Metoda 1: {ex1.Message}\n\nMetoda 2: {ex2.Message}");
                }
            }
        }

        public static async Task<bool> IsLockerClosedAsync(string sessionUuid)
        {
            if (ParcelManager.IsDebugMode) return true;

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
            if (ParcelManager.IsDebugMode)
            {
                await Task.Delay(500);
                return;
            }
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
            // METODA 1: V2 Validate (Bieżąca produkcyjna)
        private static async Task<string> OpenMethod_ValidateV2(ParcelItem parcel, double lat, double lon, string phone, string token, int retryCount)
        {
            JsonObject json = new JsonObject();
            JsonObject parcelObj = new JsonObject();
            parcelObj.SetNamedValue("shipmentNumber", JsonValue.CreateStringValue(parcel.TrackingNumber ?? ""));
            parcelObj.SetNamedValue("openCode", JsonValue.CreateStringValue(parcel.PickupCode ?? ""));
            
            JsonObject phoneObj = new JsonObject();
            phoneObj.SetNamedValue("prefix", JsonValue.CreateStringValue(parcel.PhoneNumberPrefix ?? "+48")); // Use parcel prefix if available
            phoneObj.SetNamedValue("value", JsonValue.CreateStringValue(phone ?? ""));
            parcelObj.SetNamedValue("recieverPhoneNumber", phoneObj); // Matches inpost_api.c (typo in API?)

            JsonObject geoObj = new JsonObject();
            geoObj.SetNamedValue("latitude", JsonValue.CreateNumberValue(lat));
            geoObj.SetNamedValue("longitude", JsonValue.CreateNumberValue(lon));
            geoObj.SetNamedValue("accuracy", JsonValue.CreateNumberValue(13.365));

            json.SetNamedValue("parcel", parcelObj);
            json.SetNamedValue("geoPoint", geoObj);

            using (var client = CreateHttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new Windows.Web.Http.Headers.HttpCredentialsHeaderValue("Bearer", token);
                var valContent = new HttpStringContent(json.ToString(), Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json");
                valContent.Headers.ContentType.CharSet = null;

                var validateResp = await client.PostAsync(new Uri(AppSecrets.BaseUrl + "v2/collect/validate"), valContent);

                if (validateResp.StatusCode == HttpStatusCode.Unauthorized && retryCount > 0)
                {
                    if (await ParcelManager.RefreshAuthTokenAsync())
                        return await OpenMethod_ValidateV2(parcel, lat, lon, phone, _localSettings.Values["AuthToken"].ToString(), retryCount - 1);
                }

                if (!validateResp.IsSuccessStatusCode)
                {
                    string err = await validateResp.Content.ReadAsStringAsync();
                    throw new Exception($"Błąd walidacji V2 ({validateResp.StatusCode}): {err}");
                }

                string valResponseStr = await validateResp.Content.ReadAsStringAsync();
                JsonObject valObj = JsonObject.Parse(valResponseStr);
                string sessionUuid = valObj.GetNamedString("sessionUuid");

                return await OpenCompartment(client, sessionUuid);
            }
        }

        // METODA 2: V1 Validate (Starsza / Alternatywna)
        private static async Task<string> OpenMethod_ValidateV1(ParcelItem parcel, double lat, double lon, string phone, string token)
        {
            JsonObject json = new JsonObject();
            JsonObject parcelObj = new JsonObject();
            parcelObj.SetNamedValue("shipmentNumber", JsonValue.CreateStringValue(parcel.TrackingNumber ?? ""));
            parcelObj.SetNamedValue("openCode", JsonValue.CreateStringValue(parcel.PickupCode ?? ""));
            
            JsonObject phoneObj = new JsonObject();
            phoneObj.SetNamedValue("prefix", JsonValue.CreateStringValue("+48"));
            phoneObj.SetNamedValue("value", JsonValue.CreateStringValue(phone ?? ""));
            parcelObj.SetNamedValue("recieverPhoneNumber", phoneObj); // Stara literówka (możliwa w V1)

            JsonObject geoObj = new JsonObject();
            geoObj.SetNamedValue("latitude", JsonValue.CreateNumberValue(lat));
            geoObj.SetNamedValue("longitude", JsonValue.CreateNumberValue(lon));
            geoObj.SetNamedValue("accuracy", JsonValue.CreateNumberValue(10));

            json.SetNamedValue("parcel", parcelObj);
            json.SetNamedValue("geoPoint", geoObj);

            using (var client = CreateHttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new Windows.Web.Http.Headers.HttpCredentialsHeaderValue("Bearer", token);
                var valContent = new HttpStringContent(json.ToString(), Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json");
                valContent.Headers.ContentType.CharSet = null;

                var validateResp = await client.PostAsync(new Uri(AppSecrets.BaseUrl + "v1/collect/validate"), valContent);

                if (!validateResp.IsSuccessStatusCode)
                {
                    string err = await validateResp.Content.ReadAsStringAsync();
                    throw new Exception($"Błąd walidacji V1 ({validateResp.StatusCode}): {err}");
                }

                string valResponseStr = await validateResp.Content.ReadAsStringAsync();
                JsonObject valObj = JsonObject.Parse(valResponseStr);
                string sessionUuid = valObj.GetNamedString("sessionUuid");

                return await OpenCompartment(client, sessionUuid);
            }
        }

        private static async Task<string> OpenCompartment(HttpClient client, string sessionUuid)
        {
            JsonObject openJson = new JsonObject();
            openJson.SetNamedValue("sessionUuid", JsonValue.CreateStringValue(sessionUuid));
            var openContent = new HttpStringContent(openJson.ToString(), Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json");
            openContent.Headers.ContentType.CharSet = null;

            var openResp = await client.PostAsync(new Uri(AppSecrets.BaseUrl + "v1/collect/compartment/open"), openContent);
            
            if (openResp.IsSuccessStatusCode) return sessionUuid;

            string err = await openResp.Content.ReadAsStringAsync();
            throw new Exception($"Błąd otwarcia skrytki ({openResp.StatusCode}): {err}");
        }
    }

}