using System;

namespace Mono.IO.Ports
{
    public class SerialPinChangedEventArgs : EventArgs
    {
        internal SerialPinChangedEventArgs(SerialPinChange eventType)
        {
            this.eventType = eventType;
        }

        // properties

        public SerialPinChange EventType
        {
            get
            {
                return eventType;
            }
        }

        SerialPinChange eventType;
    }
}

