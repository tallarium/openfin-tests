using Openfin.Desktop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenfinDesktop
{
    class Program
    {

        // Issues with app.Run
        static async Task MainAsync()
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            RuntimeOptions options = new RuntimeOptions();
            options.Version = "23.96.67.7";
            Runtime runtime = Openfin.Desktop.Runtime.GetRuntimeInstance(options);
            runtime.Connect(() =>
            {
                Application app = runtime.WrapApplication("openfin-closing-events-demo");

                app.isRunning((Ack ack) =>
                {
                    bool isRunning = ack.getJsonObject().Value<bool>("data");
                    Console.WriteLine("Running: " + isRunning);
                }, (Ack ack) =>
                {
                    // Error
                });
                app.Started += App_Started;
                app.Closed += App_Closed;
            });
            await taskCompletionSource.Task;
        }

        private static void App_Started(object sender, ApplicationEventArgs e)
        {
            System.Console.WriteLine("App has started");
        }

        private static void App_Closed(object sender, ApplicationEventArgs e)
        {
            Console.Out.WriteLine("App Closed");
        }

        static void Main(string[] args)
        {

            MainAsync().Wait();
            Console.ReadKey();
        }
    }
}
