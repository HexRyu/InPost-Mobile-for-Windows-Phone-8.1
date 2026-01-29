using System;
using System.Collections.Generic;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using Windows.ApplicationModel.Resources;

namespace InPost_Mobile.Models
{
    public static class TileManager
    {
        public static void Update(List<ParcelItem> allParcels)
        {
            // Re-instantiate ResourceLoader to pick up any language changes immediately
            var loader = new ResourceLoader();

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

                    // Prepare Logic for Display Name (Line 2)
                    // Priority 1: Custom Name (User-defined) -> Display just the name.
                    // Priority 2: Sender (if known and valid) -> "Paczka od {Sender}".
                    // Priority 3: Tracking Number (fallback) -> Display just the number.
                    
                    string line2Text = "";
                    if (!string.IsNullOrEmpty(p.CustomName))
                    {
                        line2Text = p.CustomName;
                    }
                    else if (!string.IsNullOrEmpty(p.Sender) && p.Sender != "Nadawca")
                    {
                        string format = loader.GetString("Tile_SenderFormat"); // "Paczka od {0}"
                        line2Text = !string.IsNullOrEmpty(format) ? string.Format(format, p.Sender) : p.Sender;
                    }
                    else
                    {
                        line2Text = p.TrackingNumber;
                    }

                    // Line 3: Status
                    // Use verified p.Status which pulls from updated resources (Gotowa do odbioru, Wydana do doręczenia)
                    string line3Text = !string.IsNullOrEmpty(p.Status) ? p.Status : p.OriginalStatus;

                    // Count Message (Header)
                    // "Paczki: X" (Localized)
                    string countFormat = loader.GetString("Tile_ParcelsHeader");
                    if (string.IsNullOrEmpty(countFormat)) countFormat = "Paczki: {0}";
                    string countMsg = string.Format(countFormat, liveCount);

                    // -- TEMPLATE: Wide310x150IconWithBadgeAndText --
                    // Attempts to replicate "Windows Central" style (Iconic).
                    // Text1: Count
                    // Text2: Name/Sender/Number
                    // Text3: Status
                    
                    var tileXml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileWide310x150IconWithBadgeAndText);
                    var textNodes = tileXml.GetElementsByTagName("text");
                    
                    if (textNodes.Length > 0) textNodes.Item(0).InnerText = countMsg;
                    if (textNodes.Length > 1) textNodes.Item(1).InnerText = line2Text;
                    if (textNodes.Length > 2) textNodes.Item(2).InnerText = line3Text;

                    // Set Icon Image
                    // Using root file 'LiveTileLogo.png' (StoreLogo content) to ensure visibility.
                    var imageNodes = tileXml.GetElementsByTagName("image");
                    if (imageNodes.Length > 0)
                    {
                        var img = imageNodes.Item(0) as XmlElement;
                        img.SetAttribute("src", "ms-appx:///LiveTileLogo.png");
                    }


                    // -- TEMPLATE: Square150x150Text02 --
                    // Layout Request:
                    // Header: "Paczki: X" (Localized)
                    // Body: Name/Sender/Number + Status (newlines)
                    
                    var squareXml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileSquare150x150Text02);
                    var sqNodes = squareXml.GetElementsByTagName("text");
                    
                    if (sqNodes.Length > 0) sqNodes.Item(0).InnerText = countMsg;
                    
                    if (sqNodes.Length > 1) 
                    {
                        string squareBody = string.Format("{0}\n{1}", line2Text, line3Text);
                        sqNodes.Item(1).InnerText = squareBody;
                    }

                    // Attempt to add Image if template supports it (Text02 usually doesn't, but we can try injecting for 'Peek' or similar if user wants logo)
                    // User asked: "formalnie loga tutaj już sie nie da dodać?"
                    // Answer: Standard Text templates don't support side-images. 
                    // To have image + text on Square, we'd need 'PeekImageAndText' (cycling) or 'IconWithBadge' (but that's for UWP).
                    // For now, sticking to clean Text layout as requested first.

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
            catch
            {
                // Errors silenced - UI may not be available (Background Task)
            }
        }
    }
}
