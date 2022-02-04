using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chromium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Remote;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenfinDesktop
{
    class TauriTests
    {
        private static readonly string HOME_PATH = (Environment.OSVersion.Platform == PlatformID.Unix ||
                   Environment.OSVersion.Platform == PlatformID.MacOSX)
    ? Environment.GetEnvironmentVariable("HOME")
    : Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");

        private static readonly string EXE_DIR = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        private static readonly string TAURI_APP_PATH = Path.Combine(EXE_DIR, @"..\..\..\..\src-tauri\target\release\OpenFin_appseed.exe");

        EdgeDriver driver;

        [SetUp]
        public void SetUp()
        {
        }

        [TearDown]
        public async Task TearDown()
        {
            StopTauriApp();
        }

        public void StartTauriApp()
        {
            var options = new EdgeOptions();
            options.UseWebView = true;
            options.BinaryLocation = TAURI_APP_PATH;
            driver = new EdgeDriver(options);
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

        [Test]
        public void CreateWindow()
        {
            StartTauriApp();

            string getWindowCount = @"
    return window.__TAURI__.window.getAll().length;
";
            long? windowCount = driver.ExecuteScript(getWindowCount) as long?;

            Assert.AreEqual(1, windowCount);

            string createWindow = @"
    done = arguments[arguments.length - 1];
    tauri = window.__TAURI__;
    w = new tauri.window.WebviewWindow('child', {
        title: 'Child',
        resizable: true,
        width: 700,
        height: 600,
        url: 'child.html'
  });
    w.once('tauri://created', function () {
        done();
    })
";
            driver.ExecuteAsyncScript(createWindow);

            windowCount = driver.ExecuteScript(getWindowCount) as long?;

            Assert.AreEqual(2, windowCount);
        }
    }
}
