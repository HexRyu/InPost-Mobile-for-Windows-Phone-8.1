using System;
using System.Collections.Generic;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using Windows.ApplicationModel.Resources;

namespace InPost_Mobile.Models
{
    public static class TileManager
    {
        private static ResourceLoader _loader = new ResourceLoader();

        public static void Update(List<ParcelItem> allParcels)
        {
            try
            {
                // STEP 1: SAFE COPY (Thread-Safe)
                List<ParcelItem> safeList = new List<ParcelItem>();
                
                if (allParcels != null)
                {
                    lock (allParcels)
                    {
                        int count = allParcels.Count;
                        for (int i = 0; i < count; i++)
                        {
                            try
                            {
                                if (i < allParcels.Count)
                                {
                                    var p = allParcels[i];
                                    if (p != null) safeList.Add(p);
                                }
                            }
                            catch { }
                        }
                    }
                }

                if (safeList.Count == 0) return;

                // STEP 2: FILTERING
                List<ParcelItem> liveParcels = new List<ParcelItem>();
                for (int i = 0; i < safeList.Count; i++)
                {
                    var p = safeList[i];
                    bool isValid = !p.IsArchived && 
                                   p.ParcelType == "Receive" && 
                                   !string.IsNullOrEmpty(p.OriginalStatus);

                    if (isValid)
                    {
                        string s = p.OriginalStatus.ToLower();
                        if (s.Contains("ready") || s.Contains("pickup_ready") || s.Contains("out_for_delivery"))
                        {
                            liveParcels.Add(p);
                        }
                    }
                }

                int liveCount = liveParcels.Count;

                // STEP 3: CLEAR TILES
                // User requested removing the "system circle" (Badge).
                // So we Clear everything initially, but we DO NOT set the Badge again.
                BadgeUpdateManager.CreateBadgeUpdaterForApplication().Clear();
                var tileUpdater = TileUpdateManager.CreateTileUpdaterForApplication();
                tileUpdater.Clear();

                if (liveCount == 0) return;

                // STEP 4: CREATE NOTIFICATIONS (First 5 Items Only)
                tileUpdater.EnableNotificationQueue(true);

                int maxItems = (liveCount > 5) ? 5 : liveCount;

                for (int i = 0; i < maxItems; i++)
                {
                    var p = liveParcels[i];

                    // Prepare Logic for Display Name
                    string displayName = !string.IsNullOrEmpty(p.CustomName) ? p.CustomName : p.Sender;
                    if (string.IsNullOrEmpty(displayName) || displayName == "Nadawca") displayName = p.TrackingNumber;
                    
                    bool isTracking = (displayName == p.TrackingNumber);
                    bool isOut = !string.IsNullOrEmpty(p.OriginalStatus) && p.OriginalStatus.ToLower().Contains("out_for_delivery");
                    
                    // Determine Status Context (Ready vs Out)
                    string statusKeyBase = isOut ? "Notif_Out_" : "Notif_Ready_";
                    
                    // Determine Content Context (Number vs Name)
                    // If it is a Tracking Number -> Use "Number" suffix (Paczka o numerze {0}...)
                    // Otherwise (Custom Name or Sender) -> Use "Name" suffix (Paczka {0}...)
                    string contentKeySuffix = isTracking ? "Number" : "Name";
                    
                    // Fetch Resource
                    string resourceKey = statusKeyBase + contentKeySuffix;
                    string formatPattern = _loader.GetString(resourceKey);
                    
                    // Construct Full Message
                    // Fallback to simple concatenation if resource is missing (safety)
                    string fullStatusMsg = "";
                    if (!string.IsNullOrEmpty(formatPattern))
                    {
                        fullStatusMsg = string.Format(formatPattern, displayName);
                    }
                    else
                    {
                        fullStatusMsg = string.Format("{0} {1}", displayName, isOut ? ">>" : "OK");
                    }

                    // Count Message
                    // "Liczba paczek: X"
                    string countLabel = _loader.GetString("Notif_ParcelCount");
                    string countMsg = string.Format("{0}: {1}", countLabel, liveCount);

                    // -- TEMPLATE: Wide310x150SmallImageAndText04 --
                    var tileXml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileWide310x150SmallImageAndText04);
                    var textNodes = tileXml.GetElementsByTagName("text");
                    
                    // Line 1: Full Status Message (Automatic Wrap)
                    // Line 2: Count
                    string combinedText = string.Format("{0}\n{1}", fullStatusMsg, countMsg);
                    if (textNodes.Length > 0) textNodes.Item(0).InnerText = combinedText;

                    // Set Image (Bottom Right)
                    var imageNodes = tileXml.GetElementsByTagName("image");
                    if (imageNodes.Length > 0)
                    {
                        var img = imageNodes.Item(0) as XmlElement;
                        img.SetAttribute("src", "Assets/Square71x71Logo.scale-100.png");
                    }


                    // -- TEMPLATE: Square150x150Text01 --
                    // Layout Request:
                    // Header: Count
                    // Body: Status
                    var squareXml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileSquare150x150Text01);
                    var sqNodes = squareXml.GetElementsByTagName("text");
                    
                    if (sqNodes.Length > 0) sqNodes.Item(0).InnerText = liveCount.ToString();
                    // For square, fullStatusMsg might be too long, but Text01 supports wrapping slightly. 
                    // Let's use it. If it truncates, it truncates.
                    if (sqNodes.Length > 1) sqNodes.Item(1).InnerText = fullStatusMsg; 
                    if (sqNodes.Length > 2) sqNodes.Item(2).InnerText = ""; // Clear 3rd line if any

                    // -- MERGE: Import Square into Wide --
                    var node = tileXml.ImportNode(squareXml.GetElementsByTagName("binding").Item(0), true);
                    tileXml.GetElementsByTagName("visual").Item(0).AppendChild(node);

                    // -- BRANDING: Set to 'name' (Bottom Left) --
                    // User requested: "W lewym dolnym rogu nazwe mojej aplikacji"
                    // "Ikonka prawy dolny" -> Not easily supported with standard Text templates, 
                    // skipping specific icon placement to favor correct text layout and stability.
                    var bindings = tileXml.GetElementsByTagName("binding");
                    for (int k = 0; k < bindings.Length; k++)
                    {
                        var binding = bindings.Item((uint)k) as XmlElement;
                        if (binding != null) binding.SetAttribute("branding", "name");
                    }

                    tileUpdater.Update(new TileNotification(tileXml));
                }
            }
            catch (Exception ex)
            {
                try 
                {
                    string msg = ex.ToString();
                    var dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher;
#pragma warning disable CS4014
                    dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                    {
                        var d = new Windows.UI.Popups.MessageDialog(msg, "TileManager Error");
                        await d.ShowAsync();
                    });
#pragma warning restore CS4014
                } catch { }
            }
        }
    }
}
