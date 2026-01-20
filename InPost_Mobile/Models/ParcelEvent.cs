using System;
using System.Runtime.Serialization;
using Windows.UI; // Potrzebne do kolorów
using Windows.UI.Xaml.Media; // Potrzebne do pędzla (Brush)

namespace InPost_Mobile.Models
{
    [DataContract]
    public class ParcelEvent
    {
        [DataMember]
        public string Date { get; set; }
        [DataMember]
        public string Description { get; set; }
        [DataMember]
        public string Color { get; set; } // To jest tekst (np. "#FFCC00")
        [DataMember]
        public double Opacity { get; set; }
        [DataMember]
        public bool IsFirst { get; set; }

        // --- TEGO BRAKOWAŁO W TWOIM PLIKU! ---
        // Ta funkcja tłumaczy tekst na kolor dla ekranu
        [IgnoreDataMember]
        public SolidColorBrush BrushColor
        {
            get
            {
                // Zabezpieczenie: jak nie ma koloru, daj żółty
                if (string.IsNullOrEmpty(Color)) return new SolidColorBrush(Colors.Orange);

                try
                {
                    string hex = Color.Replace("#", "");
                    byte a = 255;
                    byte r = 255;
                    byte g = 204;
                    byte b = 0;

                    if (hex.Length == 6)
                    {
                        r = Convert.ToByte(hex.Substring(0, 2), 16);
                        g = Convert.ToByte(hex.Substring(2, 2), 16);
                        b = Convert.ToByte(hex.Substring(4, 2), 16);
                    }
                    else if (hex.Length == 8)
                    {
                        a = Convert.ToByte(hex.Substring(0, 2), 16);
                        r = Convert.ToByte(hex.Substring(2, 2), 16);
                        g = Convert.ToByte(hex.Substring(4, 2), 16);
                        b = Convert.ToByte(hex.Substring(6, 2), 16);
                    }

                    return new SolidColorBrush(Windows.UI.Color.FromArgb(a, r, g, b));
                }
                catch
                {
                    return new SolidColorBrush(Colors.Orange);
                }
            }
        }
    }
}