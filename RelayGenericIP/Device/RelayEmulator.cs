namespace RelayGenericIP.Device
{
    using System;
	using Crestron.SimplSharp;
    using RelayGenericIP.Device;

	public class RelayEmulator
	{
        private readonly CTimer _autoOffTimer;
        private TurningOnCallbackObject _turningOnInfo = new TurningOnCallbackObject();
        private RelayState _relayState = RelayState.TurnedOff;
        private bool _autoOff = true;
		private int _autoOffTime = 30;

		public RelayEmulator()
		{
            // ReSharper disable once ObjectCreationAsStatement
            _autoOffTimer = new CTimer(AutoOffCallbackFunction, Timeout.Infinite);
        }

        public RelayState RelayState
		{
			get { return _relayState; }
            set
			{
				if (_relayState == value)
					return;

				_relayState = value;
				StateChangedEvent.Invoke(this, new StateChangeEventArgs(EventType.RelayStateChanged, _relayState));
			}
		}

        public bool AutoOff
		{
			get { return _autoOff; }
			set
			{
				if (_autoOff == value)
					return;

				_autoOff = value;
				StateChangedEvent.Invoke(this, new StateChangeEventArgs(EventType.AutoOffChanged, _autoOff));
			}
		}

		public int AutoOffTime
		{
			get { return _autoOffTime; }
			set
			{
				if (_autoOffTime == value)
					return;

				_autoOffTime = value;
				StateChangedEvent.Invoke(this, new StateChangeEventArgs(EventType.AutoOffTimeChanged, _autoOffTime));
			}
		}

		public void Off()
		{
			if (_relayState == RelayState.TurnedOff || _relayState == RelayState.TurningOff)
				return;

			// If manually turned off, cancel the auto off timer
			_autoOffTimer.Stop();

			// Set state to turing off and reset the auto off timer
			RelayState = RelayState.TurningOff;
        }

		public void Off(int delayTime)
		{
			if (_relayState == RelayState.TurnedOff || _relayState == RelayState.TurningOff)
				return;

			_autoOffTimer.Reset(delayTime * 1000 * 60);
		}

		public void On()
		{
			On(AutoOff, AutoOffTime);
		}

		public void On(bool autoOff, int autoOffTime)
		{
			if (_relayState == RelayState.TurnedOn || _relayState == RelayState.TurningOn)
				return;

			// Set the state to turning on and reset the turning on timer
			RelayState = RelayState.TurningOn;
			_turningOnInfo = new TurningOnCallbackObject {AutoOff = autoOff, AutoOffTime = autoOffTime };
        }

		private void AutoOffCallbackFunction(object notUsed)
		{
            Off();
		}

		public void TurnedOff()
		{
            RelayState = RelayState.TurnedOff;
			CreateNewRelayEvent(RelayState.TurnedOff, true, DateTime.Now);
		}

        public void TurnedOn()
		{
			RelayState = RelayState.TurnedOn;
			CreateNewRelayEvent(RelayState.TurnedOn, true, DateTime.Now);

			// If auto off is enabled, reset the timer
			if (_turningOnInfo.AutoOff)
				_autoOffTimer.Reset(_turningOnInfo.AutoOffTime * 1000 * 60);
		}

		private void CreateNewRelayEvent(RelayState eventType, bool success, DateTime time)
		{
			var relayEvent = new RelayEvent(eventType, success, time);

			StateChangedEvent.Invoke(this, new StateChangeEventArgs(EventType.RelayEvent, relayEvent));
		}

		public event EventHandler<StateChangeEventArgs> StateChangedEvent;
	}

    public class RelayEvent
    {
        public RelayEvent(RelayState eventType, bool success, DateTime time)
        {
            EventType = eventType;
            Success = success;
            Time = time;
            Summary = $"{eventType} at {time.ToShortTimeString()}";
        }

        public RelayState EventType { get; set; }
        public bool Success { get; set; }
        public DateTime Time { get; set; }
        public string Summary { get; set; }
    }
}
