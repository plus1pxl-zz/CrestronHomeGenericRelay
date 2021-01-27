namespace RelayGenericIP.Device
{
    using System;

    public class StateChangeEventArgs : EventArgs
    {
        public StateChangeEventArgs(EventType eventType, object eventData)
        {
            EventType = eventType;
            EventData = eventData;
        }

        public EventType EventType { get; private set; }

        public object EventData { get; private set; }
    }
}
