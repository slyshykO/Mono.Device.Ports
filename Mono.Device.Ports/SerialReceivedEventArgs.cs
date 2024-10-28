
using System;

namespace Mono.IO.Ports
{
    public class SerialDataReceivedEventArgs : EventArgs
    {
        internal SerialDataReceivedEventArgs(SerialData eventType)
        {
            this.eventType = eventType;
        }

        // properties

        public SerialData EventType
        {
            get
            {
                return eventType;
            }
        }

        SerialData eventType;
    }
}
