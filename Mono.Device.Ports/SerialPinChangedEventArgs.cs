using System;

namespace Mono.IO.Ports
{
    public class SerialPinChangedEventArgs : EventArgs
    {
        internal SerialPinChangedEventArgs(SerialPinChange eventType)
        {
            this.EventType = eventType;
        }

        // properties

        public SerialPinChange EventType { get; }
    }
}

