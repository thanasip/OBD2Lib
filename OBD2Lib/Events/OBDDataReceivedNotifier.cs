using OBD.NET.Devices;
using OBD.NET.OBDData;

namespace OBD2Lib.Events
{
    public abstract class OBDDataReceivedNotifier
    {
        public abstract Type DataType { get; }

        public abstract Task Notify(object sender, IOBDData data);
    }

    public class OBDDataReceivedNotifier<T> : OBDDataReceivedNotifier where T : class, IOBDData, new()
    {
        public event ELM327.DataReceivedEventHandler<T>? DataReceived;

        public override Type DataType => typeof(T);

        public override async Task Notify(object sender, IOBDData data)
            => await Task.Run(() => DataReceived?.Invoke(sender, new((data as T)!, DateTime.Now)));
    }
}
