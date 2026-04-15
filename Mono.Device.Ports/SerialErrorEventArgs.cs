
using System;

namespace Mono.IO.Ports
{
    public class SerialErrorReceivedEventArgs : EventArgs
    {

        internal SerialErrorReceivedEventArgs(SerialError eventType)
        {
            this.EventType = eventType;
        }

        // properties

        public SerialError EventType { get; }
    }
}

