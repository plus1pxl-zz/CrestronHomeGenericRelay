namespace RelayGenericIP
{
    using Crestron.RAD.Common.Transports;

    public class RelayTransport : ATransportDriver
    {
        public RelayTransport()
        {
            //IsConnected = true;
        }

        public override void SendMethod(string message, object[] parameters)
        {
        }

        public override void Start()
        {
        }

        public override void Stop()
        {
        }
	}
}
