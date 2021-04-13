using NUnit.Framework;
using Openfin.Desktop;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenfinDesktop
{
    class OpenfinTests
    {
        private const string OPENFIN_APP_UUID = "openfin-tests";

        public const string OPENFIN_ADAPTER_RUNTIME = "19.89.59.24";
        public string OPENFIN_APP_RUNTIME = "";

        private bool shareRuntime
        {
            get => OPENFIN_APP_RUNTIME == OPENFIN_ADAPTER_RUNTIME;
        }

        private const int FILE_SERVER_PORT = 9070;
        private const int REMOTE_DEBUGGING_PORT = 4444;

        private static readonly string FILE_SERVER_ROOT_URL = String.Format("http://localhost:{0}/", FILE_SERVER_PORT);
        private static readonly string APP_CONFIG_URL = FILE_SERVER_ROOT_URL + "app.json";

        ChromeDriver driver;
        HttpFileServer fileServer;

        Runtime runtime;

        [SetUp]
        public void SetUp()
        {
            string dir = Path.GetDirectoryName(GetType().Assembly.Location);
            string dirToServe = Path.Combine(dir, "../../../../src");
            // Serve OpenFin app assets
            fileServer = new HttpFileServer(dirToServe, FILE_SERVER_PORT);
            RuntimeOptions appOptions = RuntimeOptions.LoadManifest(new Uri(APP_CONFIG_URL));
            OPENFIN_APP_RUNTIME = appOptions.Version;
        }

        public void StartOpenfinApp()
        {
            string dir = Path.GetDirectoryName(GetType().Assembly.Location);

            var service = ChromeDriverService.CreateDefaultService();
            service.LogPath = Path.Combine(dir, "chromedriver.log");
            service.EnableVerboseLogging = true;

            var options = new ChromeOptions();

            string runOpenfinPath = Path.Combine(dir, "RunOpenFin.bat");
            string appConfigArg = String.Format("--config={0}", APP_CONFIG_URL);
            if (shareRuntime && runtime != null)
            {
                options.DebuggerAddress = "localhost:4444";
                Process.Start(runOpenfinPath, appConfigArg);
            } else
            {
                options.BinaryLocation = runOpenfinPath;
                options.AddArgument(appConfigArg);
                options.AddArgument(String.Format("--remote-debugging-port={0}", REMOTE_DEBUGGING_PORT));
            }
            driver = new ChromeDriver(service, options);
        }

        private async Task<Runtime> ConnectToRuntime()
        {
            String arguments = shareRuntime ? String.Format("--remote-debugging-port={0}", REMOTE_DEBUGGING_PORT) : "";

            runtime = await OpenfinHelpers.ConnectToRuntime(OPENFIN_ADAPTER_RUNTIME, arguments);

            return runtime;
        }

        private async Task<Application> GetApplication(string UUID)
        {
            Runtime runtime = await ConnectToRuntime();
            return runtime.WrapApplication(UUID);
        }

        private Task<bool> AppIsRunning(Application app)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();

            app.isRunning((Ack ack) =>
            {
                bool isRunning = ack.getJsonObject().Value<bool>("data");
                taskCompletionSource.SetResult(isRunning);
            }, (Ack ack) =>
            {
                // Error
                taskCompletionSource.SetException(new Exception(ack.getJsonObject().ToString()));
            });
            return taskCompletionSource.Task;
        }

        private async Task<bool> IsEventually(Func<bool> getState, bool expectedState, int timeout)
        {
            CancellationTokenSource cancellationToken = new CancellationTokenSource();
            Task<bool> checkIsRunningTask = Task.Run<bool>(async () =>
            {
                bool currentState = !expectedState;
                while (!cancellationToken.IsCancellationRequested && currentState != expectedState)
                {
                    await Task.Delay(100);
                    currentState = getState();
                }
                return currentState;
            });

            Task timeoutTask = Task.Delay(timeout, cancellationToken.Token);
            await Task.WhenAny(checkIsRunningTask, timeoutTask);

            cancellationToken.Cancel();

            return await checkIsRunningTask;
        }

        private async Task<bool> AppIsEventuallyRunning(Application app, bool expectedState, int timeout)
        {
            return await IsEventually(() =>
            {
                Task<bool> isRunningTask = AppIsRunning(app);
                isRunningTask.Wait();
                return isRunningTask.Result;
            }, expectedState, timeout);
        }

        /* Would prefer to use for inspecting and resizing windows but driver currently raises an exception
         * e.g.
         *         public void ResizeUsingChromeDriver()
        {
            StartOpenfinApp();
            IWindow window = driver.Manage().Window;
            Point initialPos = window.Position; // Crashes here - Browser window not found
            Point newPos = new Point(initialPos.X + 75, initialPos.Y + 180);
            window.Position = newPos;
        }
         * Use OpenFin Javasript API instead
        */
        private Dictionary<string, object> getWindowBounds()
        {
            var script = $@"
const app = fin.Application.getCurrentSync();
// const win = await app.getWindow(); Not actually the app window.  Probably the hidden Provider window
const childWindows = await app.getChildWindows()
const childWin = childWindows[0];
const bounds = await childWin.getBounds();
return bounds;
";
            return driver.ExecuteScript(script) as Dictionary<string, object>;
        }

        private void setWindowBounds(int left, int top, int width, int height)
        {
            var script = $@"
const app = fin.Application.getCurrentSync();
// const win = await app.getWindow(); Not actually the app window.  Probably the hidden Provider window
const childWindows = await app.getChildWindows()
const childWin = childWindows[0];
const bounds = {{
    height: {height},
    width: {width},
    top: {top},
    left: {left}
}}
    await childWin.setBounds(bounds);
";
            driver.ExecuteScript(script);
        }

        [Test]
        public async Task IsRunningInitiallyClosed()
        {

            Application app = await GetApplication(OPENFIN_APP_UUID);
            bool isRunning = await AppIsEventuallyRunning(app, false, 1000);

            Assert.IsFalse(isRunning, "App isRunning (Initially)");
            StartOpenfinApp();
            isRunning = await AppIsEventuallyRunning(app, true, 1000);
            Assert.IsTrue(isRunning, "App isRunning (After start)");
            StopOpenfinApp();
            isRunning = await AppIsEventuallyRunning(app, false, 1000);
            Assert.IsFalse(isRunning, "App isRunning (After stop)");
        }

        [Test]
        public async Task IsRunningInitiallyOpen()
        {

            StartOpenfinApp();

            Application app = await GetApplication(OPENFIN_APP_UUID);

            bool isRunning = await AppIsEventuallyRunning(app, true, 1000);

            Assert.IsTrue(isRunning, "App isRunning (Initially)");
            StopOpenfinApp();
            isRunning = await AppIsEventuallyRunning(app, false, 1000);
            Assert.IsFalse(isRunning, "App isRunning (After Stop)");
            StartOpenfinApp();
            isRunning = await AppIsEventuallyRunning(app, true, 1000);
            Assert.IsTrue(isRunning, "App isRunning (After Start)");
        }

        [Test]
        public async Task AppEventsInitiallyClosed()
        {
            bool startedFired = false;
            bool closedFired = false;

            Application app = await GetApplication(OPENFIN_APP_UUID);
            app.Started += (object sender, ApplicationEventArgs e) =>
            {
                startedFired = true;
            };

            app.Closed += (object sender, ApplicationEventArgs e) =>
            {
                closedFired = true;
            };

            StartOpenfinApp();
            await IsEventually(() => { return startedFired; }, true, 500);
            Assert.IsTrue(startedFired, "'Started' event is fired");
            StopOpenfinApp();
            await IsEventually(() => { return closedFired; }, true, 500);
            Assert.IsTrue(closedFired, "'Closed' event is fired");
        }

        [Test]
        public async Task AppEventsInitiallyOpen()
        {
            bool startedFired = false;
            bool closedFired = false;

            StartOpenfinApp();

            Application app = await GetApplication(OPENFIN_APP_UUID);
            app.Started += (object sender, ApplicationEventArgs e) =>
            {
                startedFired = true;
            };

            app.Closed += (object sender, ApplicationEventArgs e) =>
            {
                closedFired = true;
            };

            StopOpenfinApp();
            await IsEventually(() => { return closedFired; }, true, 500);
            Assert.IsTrue(closedFired, "'Closed' event is fired");
            StartOpenfinApp();
            await IsEventually(() => { return startedFired; }, true, 500);
            Assert.IsTrue(startedFired, "'Started' event is fired");
        }

        private Dictionary<string, object> getProcessInfo()
        {
            string script = "return await fin.System.getProcessList()";
            driver.ExecuteScript(script); // First call is different to following calls
            dynamic processList = driver.ExecuteScript(script);
            return processList[0] as Dictionary<string, object>;
        }

        // TODO: Pending fix from OpenFin - https://openfin.zendesk.com/hc/requests/11460
        //[Test]
        //public async Task GetProcessList()
        //{
        //    StartOpenfinApp();

        //    var processInfo = getProcessInfo();
        //    long origWorkingSetSize = (long)processInfo["workingSetSize"];

        //    Assert.Greater(origWorkingSetSize, 10000000, "working set at least 10MB");

        //    driver.ExecuteScript("window.location = 'http://www.google.co.uk'");
        //    await Task.Delay(2000);

        //    processInfo = getProcessInfo();
        //    long workingSetSize = (long)processInfo["workingSetSize"];

        //    Assert.Greater(workingSetSize, 10000000, "working set at least 10MB");

        //    string returnLocationScript = String.Format("window.location = '{0}index.html'", FILE_SERVER_ROOT_URL);
        //    driver.ExecuteScript(returnLocationScript);
        //    await Task.Delay(2000);

        //    processInfo = getProcessInfo();
        //    workingSetSize = (long)processInfo["workingSetSize"];

        //    Assert.Greater(workingSetSize, 10000000, "working set at least 10MB");
        //    Assert.Greater(workingSetSize, origWorkingSetSize * 0.7, "Similar size to original working set");
        //    Assert.Less(workingSetSize, origWorkingSetSize * 1.3, "Similar size to original working set");
        //}

        [Test]
        public void AppHasDefaultSize()
        {
            StartOpenfinApp();

            var bounds = getWindowBounds();
            Assert.AreEqual(600, bounds["width"]);
            Assert.AreEqual(600, bounds["height"]);
        }

        [Test]
        public void ResizeWindow()
        {
            StartOpenfinApp();
            setWindowBounds(100, 150, 200, 300);
            var bounds = getWindowBounds();
            Assert.AreEqual(100, bounds["left"]);
            Assert.AreEqual(150, bounds["top"]);
            Assert.AreEqual(200, bounds["width"]);
            Assert.AreEqual(300, bounds["height"]);

        }

        [Test]
        public void RestoreSnapshot()
        {
            StartOpenfinApp();
            int newLeft = 100;
            int newTop = 150;
            int newWidth = 200;
            int newHeight = 300;
            setWindowBounds(newLeft, newTop, newWidth, newHeight);

            string createSnapshotScript = $@"
    const platform = await fin.Platform.getCurrent();
    const snapshot = await platform.getSnapshot(); // Raises 'no action registered' exception
    return JSON.stringify(snapshot)
";
            string snapshot = driver.ExecuteScript(createSnapshotScript) as string;
            StopOpenfinApp();
            StartOpenfinApp();

            // Reloads with default size
            var bounds = getWindowBounds();
            Assert.AreEqual(600, bounds["width"]);
            Assert.AreEqual(600, bounds["height"]);

            string escapedSnapshot = snapshot.Replace(@"\", @"\\"); // "\"s will be unescaped when injected into script string

            string restoreSnapshotScript = $@"
const platform = await fin.Platform.getCurrent();
const snapshot = JSON.parse('{escapedSnapshot}');  // Raises 'no action registered' exception
await platform.applySnapshot(snapshot);
";
            driver.ExecuteScript(restoreSnapshotScript);

            bounds = getWindowBounds();
            Assert.AreEqual(newLeft, bounds["left"]);
            Assert.AreEqual(newTop, bounds["top"]);
            Assert.AreEqual(newWidth, bounds["width"]);
            Assert.AreEqual(newHeight, bounds["height"]);
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
            fileServer?.Stop();

            StopOpenfinApp();

            await OpenfinHelpers.DisconnectFromRuntime(runtime);
        }
    }
}
