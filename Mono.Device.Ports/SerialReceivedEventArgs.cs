
using System;

namespace Mono.IO.Ports
{
    public class SerialDataReceivedEventArgs : EventArgs
    {
        internal SerialDataReceivedEventArgs(SerialData eventType)
        {
            this.EventType = eventType;
        }

        // properties

        public SerialData EventType { get; }
    }
}
