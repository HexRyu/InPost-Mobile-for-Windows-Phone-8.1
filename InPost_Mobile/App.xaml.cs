using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Animation;
using Windows.Storage;
using Windows.Phone.UI.Input; // Obsługa fizycznego przycisku

namespace InPost_Mobile
{
    public sealed partial class App : Application
    {
        private TransitionCollection transitions;

        public App()
        {
            InPost_Mobile.Helpers.LanguageManager.InitializeLanguage();

            this.InitializeComponent();
            this.Suspending += this.OnSuspending;

            // Obsługa fizycznego przycisku "Wstecz"
            HardwareButtons.BackPressed += HardwareButtons_BackPressed;
        }

        // --- OBSŁUGA COFANIA ---
        private void HardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame != null && rootFrame.CanGoBack)
            {
                e.Handled = true;
                rootFrame.GoBack();
            }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif

            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.CacheSize = 1;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    // TODO: Load state from previously suspended application
                }

                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                if (rootFrame.ContentTransitions != null)
                {
                    this.transitions = new TransitionCollection();
                    foreach (var c in rootFrame.ContentTransitions)
                    {
                        this.transitions.Add(c);
                    }
                }

                rootFrame.ContentTransitions = null;
                rootFrame.Navigated += this.RootFrame_FirstNavigated;

                var settings = ApplicationData.Current.LocalSettings;
                bool isLoggedIn = InPost_Mobile.Models.ParcelManager.IsLoggedIn();
                bool isSetupDone = settings.Values.ContainsKey("IsSetupDone");

                if (isLoggedIn || isSetupDone)
                {
                    if (!rootFrame.Navigate(typeof(InPost_Mobile.Views.MainPage), e.Arguments))
                    {
                        throw new Exception("Failed to create initial page");
                    }
                }
                else
                {
                    if (!rootFrame.Navigate(typeof(InPost_Mobile.Views.LoginPage), e.Arguments))
                    {
                        throw new Exception("Failed to create initial page");
                    }
                }
            }

            Window.Current.Activate();
        }

        private void RootFrame_FirstNavigated(object sender, NavigationEventArgs e)
        {
            var rootFrame = sender as Frame;
            rootFrame.ContentTransitions = this.transitions ?? new TransitionCollection() { new NavigationThemeTransition() };
            rootFrame.Navigated -= this.RootFrame_FirstNavigated;
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}