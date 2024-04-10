using OBD.NET.Commands;
using OBD.NET.Events;
using OBD.NET.OBDData;
using OBD2Lib.Scanners;

namespace OBD2LibExample
{
    internal class Program
    {
        private readonly static List<ATCommand> _initSequence =
        [
            ATCommand.ResetDevice,
            ATCommand.EchoOff,
            ATCommand.LinefeedsOff,
            ATCommand.HeadersOff,
            ATCommand.PrintSpacesOff,
            ATCommand.SetProtocolAuto
        ];

        private static ELMScanner? _scanner;

        static async Task Main()
        {
            _scanner = new("COM4", 115200, true);

            await _scanner.InitializeAsync(_initSequence);

            _scanner.Subscribe<EngineRPM>(EngineRPMHandler);

            _scanner.StartPollingPids([
                typeof(EngineRPM),
                typeof(ThrottlePosition),
                typeof(EngineCoolantTemperature)
            ]);

            await Task.Delay(2000);

            _scanner.Unsubscribe<EngineRPM>(EngineRPMHandler);

            _scanner.StopPollingPids();

            await Task.Delay(2000);

            _scanner.Subscribe<ThrottlePosition>(ThrottlePositionHandler);

            _scanner.StartPollingPids([
                typeof(EngineRPM),
                typeof(ThrottlePosition),
                typeof(EngineCoolantTemperature)
            ]);

            await Task.Delay(2000);

            _scanner.Unsubscribe<ThrottlePosition>(ThrottlePositionHandler);

            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(200);
                Console.WriteLine($"Got Engine RPM: {_scanner.GetLatest<EngineRPM>()}");
                Console.WriteLine($"Got Throttle Position: {_scanner.GetLatest<ThrottlePosition>()}");
            }

            _scanner.StopPollingPids();

            await Task.Delay(2000);

            Console.WriteLine($"Async direct response query of EngineRPM: {await _scanner.QueryPIDAsync<EngineRPM>()}");

            _scanner.Subscribe<EngineRPM>((_, e) => Console.WriteLine($"Sync handler-invoking query of EngineRPM: {e.Data.Rpm}"));

            _scanner.QueryPID<EngineRPM>();

            Console.ReadKey(true);
        }

        static void EngineRPMHandler(object _, DataReceivedEventArgs<EngineRPM> e)
            => Console.WriteLine($"Got Engine RPM: {e.Data.Rpm}");

        static void ThrottlePositionHandler(object _, DataReceivedEventArgs<ThrottlePosition> e)
            => Console.WriteLine($"Got Throttle Position: {e.Data.Position}");
    }
}
