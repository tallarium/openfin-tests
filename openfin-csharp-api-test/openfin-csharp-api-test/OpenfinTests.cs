using NUnit.Framework;
using Openfin.Desktop;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
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
    class OpenfinTests
    {
        private const string OPENFIN_APP_UUID = "openfin-closing-events-demo";

        ChromeDriver driver;
        HttpFileServer fileServer;

        Runtime runtime;

        [SetUp]
        public void SetUp()
        {
            string dir = Path.GetDirectoryName(GetType().Assembly.Location);
            string dirToServe = Path.Combine(dir, "../../../../src");
            fileServer = new HttpFileServer(dirToServe, 9070);
        }

        public void StartOpenfinApp()
        {
            string dir = Path.GetDirectoryName(GetType().Assembly.Location);

            var service = ChromeDriverService.CreateDefaultService();
            service.LogPath = Path.Combine(dir, "chromedriver.log");
            service.EnableVerboseLogging = true;

            var options = new ChromeOptions();

            string runOpenfinPath = Path.Combine(dir, "RunOpenFin.bat");
            string appConfigArg = "--config=http://localhost:9070/app.json";
            if (runtime != null)
            {
                options.DebuggerAddress = "localhost:4444";
                Process.Start(runOpenfinPath, appConfigArg);
            } else
            {
                options.BinaryLocation = runOpenfinPath;
                options.AddArgument(appConfigArg);
                options.AddArgument("--remote-debugging-port=4444");
            }
            driver = new ChromeDriver(service, options);
        }

        private Task<Runtime> ConnectToRuntime()
        {
            var taskCompletionSource = new TaskCompletionSource<Runtime>();

            RuntimeOptions options = new RuntimeOptions();
            options.Version = "23.96.67.7";
            options.Arguments = "--remote-debugging-port=4444";
            runtime = Openfin.Desktop.Runtime.GetRuntimeInstance(options);
            runtime.Connect(() =>
            {
                taskCompletionSource.SetResult(runtime);
            });

            return taskCompletionSource.Task;
        }

        private Task<Runtime> DisconnectFromRuntime()
        {
            var taskCompletionSource = new TaskCompletionSource<Runtime>();

            if (runtime != null && runtime.IsConnected)
            {
                runtime.Disconnect(() =>
                {
                    taskCompletionSource.SetResult(runtime);
                });
            }
            else
            {
                taskCompletionSource.SetResult(runtime);
            }

            return taskCompletionSource.Task;
        }

        [Test]
        public void ResizeUsingChromeDriver()
        {
            StartOpenfinApp();
            IWindow window = driver.Manage().Window;
            Point initialPos = window.Position; // Crashes here - Browser window not found
            Point newPos = new Point(initialPos.X + 75, initialPos.Y + 180);
            window.Position = newPos;
            driver.ExecuteScript("alert('Done')");
        }

        [Test]
        public void ResizeUsingOpenfinAPI()
        {
            StartOpenfinApp();
            var script = $@"
alert('Resizing')
const app = fin.Application.getCurrentSync();
const win = await app.getWindow();
const bounds = {{
    height: 100,
    width: 200,
    top: 400,
    left: 400
}}
await win.setBounds(bounds);
alert('Resized');
";
            driver.ExecuteScript(script);
        }

        public void StopOpenfinApp()
        {
            if (driver != null)
            {
                // Neither of the below actually close the OpenFin runtime
                //driver.Close();
                //driver.Quit();
                driver.ExecuteScript("window.close()"); // This does
                driver.Quit();
            }
            driver = null;
        }

        [TearDown]
        public async Task TearDown()
        {
            if (fileServer != null)
            {
                fileServer.Stop();
            }
            StopOpenfinApp();

            await DisconnectFromRuntime();
        }
    }
}
