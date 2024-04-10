using OBD.NET.Commands;
using OBD.NET.Communication;
using OBD.NET.Devices;
using OBD.NET.Events;
using OBD.NET.OBDData;
using OBD2Lib.Events;
using System.IO.Ports;

namespace OBD2Lib.Scanners
{
    public class ELMScanner
    {
        private readonly static List<Type> _pidTypes = [];

        static ELMScanner()
        {
            _pidTypes = typeof(IOBDData).Assembly
                .GetTypes()
                .Where(t => t?.BaseType == typeof(AbstractOBDData) && !t.IsAbstract)
                .ToList();
        }

        private readonly SerialConnection _connection;
        private readonly ELM327 _scanner;
        private readonly List<OBDDataReceivedNotifier> _notifiers = [];
        private readonly Dictionary<Type, IOBDData?> _dataByPid = [];

        private bool _polling = false;
        private bool _pollingCallbacks = false;
        private CancellationTokenSource? _cts;

        public ELMScanner(string port, int baudRate)
        {
            _connection = new SerialConnection(port, baudRate, Parity.None, StopBits.One, Handshake.None);
            _scanner = new ELM327(_connection);
            _scanner.SubscribeDataReceived<IOBDData>(InternalOBDDataReceived);

            var openType = typeof(OBDDataReceivedNotifier<>);
            _pidTypes.ForEach(pid => 
            { 
                var closedType = openType.MakeGenericType(pid);
                var instance = Activator.CreateInstance(closedType);
                _notifiers.Add((instance as OBDDataReceivedNotifier)!);
            });
        }

        public bool FireCallBacksWhenPolling
        {
            get => _pollingCallbacks;
            set => _pollingCallbacks = value;
        }

        public ELMScanner(string port, int baudRate, bool fireEventsOnPoll) : this(port, baudRate)
        {
            _pollingCallbacks = fireEventsOnPoll;
        }

        public void Initialize(List<ATCommand>? customSequence = null)
        {
            if (customSequence is null)
                _scanner.Initialize();
            else
                _scanner.CustomInitialize(customSequence);
        }

        public async Task InitializeAsync(List<ATCommand>? customSequence = null)
        {
            if (customSequence is null)
                await _scanner.InitializeAsync();
            else
                await _scanner.CustomInitializeAsync(customSequence);
        }

        public void StartPollingPids(List<Type> polledPids)
        {
            if (!_polling)
            {
                _polling = true;

                polledPids.ForEach(pid =>
                {
                    if (!pid.GetInterfaces().Any(iface => iface == typeof(IOBDData)))
                    {
                        _dataByPid.Clear();
                        throw new Exception($"{pid.Name} does not implement IOBDData.");
                    }
                    else
                    {
                        if (!_dataByPid.TryAdd(pid, null))
                            throw new Exception($"Error adding {pid.Name} to polled PIDs.");
                    }
                });

                Task.Run(PollPids, (_cts = new()).Token);
            }
            else
            {
                throw new Exception("Call \"StopPollingPids()\" before changing polled PIDs.");
            }
        }

        public void StopPollingPids()
        {
            _cts?.Cancel();   //Request cancellation via the TokenSource
            Thread.Sleep(50); //Give the poll Task a bit of time to respond
            _cts?.Dispose();  //Dispose of the TokenSource
        }

        public T? GetLatest<T>() where T : class, IOBDData, new()
        {
            if (_dataByPid.TryGetValue(typeof(T), out var data))
                return (T?)data;

            return null;
        }

        public async Task<T?> QueryPIDAsync<T>() where T : class, IOBDData, new()
        {
            if (await _scanner.RequestDataAsync(typeof(T)) is IOBDData data)
                return (T?)data;

            return null;
        }

        public void QueryPID<T>() where T : class, IOBDData, new()
            => _scanner.RequestData<T>();

        public void QueryPID(Type pid)
            => _scanner.RequestData((Activator.CreateInstance(pid) as IOBDData)!.PID);

        public void Subscribe<T>(ELM327.DataReceivedEventHandler<T> handler) where T : class, IOBDData, new()
        {
            if (GetNotifier<T>() is OBDDataReceivedNotifier<T> notifier)
                notifier.DataReceived += handler;
        }

        public void Unsubscribe<T>(ELM327.DataReceivedEventHandler<T> handler) where T : class, IOBDData, new()
        {
            if (GetNotifier<T>() is OBDDataReceivedNotifier<T> notifier)
                notifier.DataReceived -= handler;
        }

        private async void PollPids()
        {
            var errors = 0;
            var notifiers = _notifiers
                .Where(n => _dataByPid.ContainsKey(n.DataType))
                .ToDictionary(n => n.DataType);

            while (true)
            {
                if (_cts!.Token.IsCancellationRequested)
                {
                    _dataByPid.Clear();
                    _polling = false;
                    return;
                }

                try
                {
                    for (var i = 0; i < _dataByPid.Keys.Count; i++)
                    {
                        var pid = _dataByPid.Keys.ElementAt(i);

                        if (pid is not null && await _scanner.RequestDataAsync(pid) is IOBDData data)
                        {
                            _dataByPid[pid] = data;

                            if (_pollingCallbacks && notifiers[pid] is OBDDataReceivedNotifier notifier)
                                await notifier.Notify(this, data);
                        }
                    }
                }
                catch
                {
                    //Just try again, unless we hit a total of 10 errors
                    if (++errors == 10) throw;
                }
            }
        }

        private async void InternalOBDDataReceived(object sender, DataReceivedEventArgs<IOBDData> e)
        {
            if (GetNotifier(e.Data.GetType()) is OBDDataReceivedNotifier notifier)
                await notifier.Notify(sender, e.Data);
        }

        private OBDDataReceivedNotifier? GetNotifier<T>() where T : class, IOBDData, new()
            => _notifiers?.FirstOrDefault(n => n.DataType == typeof(T)) is OBDDataReceivedNotifier notifier
                ? notifier
                : null;

        private OBDDataReceivedNotifier? GetNotifier(Type obdDataType)
            => _notifiers?.FirstOrDefault(n => n.DataType == obdDataType) is OBDDataReceivedNotifier notifier
                ? notifier
                : null;
    }
}
