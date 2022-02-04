using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chromium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Remote;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenfinDesktop
{
    // Based on https://github.com/SeleniumHQ/selenium/blob/trunk/dotnet/src/webdriver/Edge/EdgeOptions.cs
    // and https://tauri.studio/docs/guides/webdriver/example/selenium/#testing
    class TauriOptions : ChromiumOptions
    {
        private Dictionary<string, object> tauriOptions = new Dictionary<string, object>();

        public TauriOptions(string applicationPath) : base()
        {
            this.BrowserName = "wry";
            Dictionary<string, object> tauriOptions = new Dictionary<string, object>();
            this.AddAdditionalChromiumOption("application", applicationPath);
        }

        /// <summary>
        /// Gets the vendor prefix to apply to Chromium-specific capability names.
        /// </summary>
        protected override string VendorPrefix
        {
            get { return "tauri"; }
        }

        public override string CapabilityName
        {
            get { return "tauri:options"; }
        }
    }

    class TauriTests
    {
        private static readonly string HOME_PATH = (Environment.OSVersion.Platform == PlatformID.Unix ||
                   Environment.OSVersion.Platform == PlatformID.MacOSX)
    ? Environment.GetEnvironmentVariable("HOME")
    : Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");

        private static readonly string TAURI_DRIVER_PATH = Path.Combine(HOME_PATH, ".cargo", "bin", "tauri-driver");
        private static readonly string EXE_DIR = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        private static readonly string TAURI_APP_PATH = Path.Combine(EXE_DIR, @"..\..\..\..\src-tauri\target\release\OpenFin_appseed.exe");

        Process driverProcess;
        RemoteWebDriver driver;

        public TauriTests()
        {
            string path = System.Environment.GetEnvironmentVariable("PATH");
            System.Environment.SetEnvironmentVariable("PATH", path + ";" + EXE_DIR);
        }

        [SetUp]
        public void SetUp()
        {
            driverProcess = Process.Start(TAURI_DRIVER_PATH);
        }

        [TearDown]
        public async Task TearDown()
        {
            StopTauriApp();
            driverProcess?.Kill();
        }

        public void StartTauriApp()
        {
            ChromiumOptions options = new TauriOptions(TAURI_APP_PATH);
            driver = new RemoteWebDriver(new Uri("http://localhost:4444/"), options);
        }

        public void StopTauriApp()
        {
            driver?.Quit();
            driver = null;
        }

        [Test]
        public void Basic()
        {
            StartTauriApp();


            string testForTauri = $@"
    return !!window.__TAURI__;
";
            object value = driver.ExecuteScript(testForTauri);
            Assert.AreEqual(true, value);
        }

    }
}
