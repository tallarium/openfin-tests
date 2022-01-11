using NUnit.Framework;
using Openfin.Desktop;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OpenfinDesktop
{
    class OpenfinTests
    {
        private const string OPENFIN_APP_UUID = "openfin-tests";

        public const string OPENFIN_ADAPTER_RUNTIME = "23.96.67.7";
        public string OPENFIN_APP_RUNTIME = "";

        public int APP_WINDOW_LOAD_TIMEOUT_MS = 30000;

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

        private Task<bool> WindowsWereCreated(Application app)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();

            app.getChildWindows((children) =>
            {
                taskCompletionSource.SetResult(children?.Count > 0);
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

        private async Task<bool> WindowIsEventuallyOpen(Application app, int timeout)
        {
            return await IsEventually(() =>
            {
                Task<bool> windowsWereCreatedCheck = WindowsWereCreated(app);
                windowsWereCreatedCheck.Wait();
                return windowsWereCreatedCheck.Result;
            }, true, timeout);
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
