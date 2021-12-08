namespace RelayGenericIP
{
    using System;
    using System.Linq;
    using Crestron.RAD.Common;
    using Crestron.RAD.Common.Attributes.Programming;
    using Crestron.RAD.Common.Enums;
    using Crestron.RAD.Common.Events;
    using Crestron.RAD.Common.Interfaces;
    using Crestron.RAD.Common.Interfaces.ExtensionDevice;
    using Crestron.RAD.Common.Transports;
    using Crestron.RAD.DeviceTypes.ExtensionDevice;
    using Crestron.SimplSharp;
    using Crestron.SimplSharpPro.CrestronThread;
    using RelayGenericIP.Device;

    public class RelayDriverIp : AExtensionDevice,
			//ICloudConnected,
            ITcp
    {
        private const int PollingInterval = 60000;

        #region Commands

		// Define all of the keys to be used as commands, these match the keys in the ui definition for command actions
		private const string OnCommand = "PowerOn";
		private const string OffCommand = "PowerOff";
		private const string ToggleCommand = "PowerToggle";

		#endregion

		#region Property Keys

		// Define all of the keys for properties, these match the properties used in the ui definition
		private const string RelayStateIconKey = "RelayStateIcon";
        private const string ErrorIcon = "icAlertRegular";
		//private const string OnIcon = "icRemoteButtonGreen";
		//private const string OffIcon = "icRemoteButtonRed";
		private const string SpinnerIcon = "icSpinner";
		private const string RelayStateKey = "RelayState";
		private const string TurnedOnLabel = "^TurnedOnLabel";
		private const string TurningOnLabel = "^TurningOnLabel";
		private const string TurnedOffLabel = "^TurnedOffLabel";
		private const string TurningOffLabel = "^TurningOffLabel";
        private const string ErrorLabel = "^ErrorLabel";

		private const string AutoOffKey = "AutoOff";
		private const string AutoOffTimeKey = "AutoOffTime";

		#endregion

		#region Translation Keys

		// Define all of the keys to be used for translations, these match the keys in the translation files
		private const string RelayStateTranslationKey = "OnRelayStateLabel";
		private const string AutoOffTranslationKey = "AutoOffLabel";
		private const string AutoOffTimeTranslationKey = "AutoOffTimeLabel";

		#endregion

        #region Programming

		private const string OnLabel = "^OnLabel";
		private const string OffWithDelayLabel = "^OffWithDelayLabel";
		private const string DelayLabel = "^DelayLabel";
		private const string OffLabel = "^OffLabel";
		private const string OnWithAutoOffLabel = "^OnWithAutoOffLabel";
		private const string AutoOffTimeLabel = "^AutoOffTimeLabel";
		private const string EnableAutoOffLabel = "^EnableAutoOffLabel";
		private const string DisableAutoOffLabel = "^DisableAutoOffLabel";
		private const string SetAutoOffTimeLabel = "^SetAutoOffTimeLabel";

		#endregion

		#region Fields

        private bool _consoleDebuggingEnabled;
		private string _onIcon;
        private string _offIcon;

		private RelayEmulator _relayEmulator;
		private RelayProtocol _relayProtocol;
        private RelayTransport _relayTransport;
		TcpTransport _tcpTransport;

		private PropertyValue<string> _relayStateProperty;
		private PropertyValue<string> _relayStateIconProperty;
		private PropertyValue<bool> _autoOffProperty;
		private PropertyValue<int> _autoOffTimeProperty;

		#endregion

		#region Constructor

		public RelayDriverIp()
			: base()
        {
            this._consoleDebuggingEnabled = true;
			AddUserAttributes();
			CreateDeviceDefinition();
		}

		#endregion

		#region Property
		protected RelayProtocol Protocol
		{
			get { return _relayProtocol; }
			set
			{
				if (_relayProtocol != null)
				{
					_relayProtocol.ConnectedChanged -= ProtocolConnectedChanged;
				}

				_relayProtocol = value;
				DeviceProtocol = _relayProtocol;

				if (value != null)
				{
					_relayProtocol.ConnectedChanged -= ProtocolConnectedChanged;
					_relayProtocol.ConnectedChanged += ProtocolConnectedChanged;
				}
			}
		}

		#endregion

		#region AExtensionDevice Members

		protected override IOperationResult SetDriverPropertyValue<T>(string propertyKey, T value)
		{
			switch (propertyKey)
			{
				case AutoOffKey:
					var autoOff = value as bool?;
					if (autoOff == null)
						return new OperationResult(OperationResultCode.Error, "The value provided could not be converted to a bool.");

					_relayEmulator.AutoOff = (bool)autoOff;
					return new OperationResult(OperationResultCode.Success);

				case AutoOffTimeKey:
					var autoOffTime = value as int?;
					if (autoOffTime == null)
						return new OperationResult(OperationResultCode.Error, "The value provided could not be converted to an int.");

					_relayEmulator.AutoOffTime = (int)autoOffTime;
					return new OperationResult(OperationResultCode.Success);
			}

			return new OperationResult(OperationResultCode.Error, "The property does not exist.");
		}

		protected override IOperationResult SetDriverPropertyValue<T>(string objectId, string propertyKey, T value)
		{
			throw new System.NotImplementedException();
		}


		protected override IOperationResult DoCommand(string command, string[] parameters)
		{
			// ReSharper disable once ObjectCreationAsStatement
			new Thread(DoCommandThreadCallback, new DoCommandObject(command, parameters));
			return new OperationResult(OperationResultCode.Success);
		}

		#endregion

		#region ITcp Members

		void ITcp.Initialize(IPAddress ipAddress, int port)
		{
			Initialize(ipAddress, port);
		}

		public void Initialize(IPAddress ipAddress, int port)
		{

			try
			{
				_tcpTransport = new TcpTransport
				{
					EnableAutoReconnect = this.EnableAutoReconnect,
					EnableLogging = this.InternalEnableLogging,
					CustomLogger = this.InternalCustomLogger,
					EnableRxDebug = this.InternalEnableRxDebug,
					EnableTxDebug = this.InternalEnableTxDebug
				};

				TcpTransport tcpTransport = _tcpTransport;
				tcpTransport.Initialize(ipAddress, port);
				this.ConnectionTransport = tcpTransport;
				tcpTransport.DriverID = DriverID;
                base.UserAttributesChanged += RelayDriverIpUserAttributesChanged;

				_relayProtocol = new RelayProtocol(this.ConnectionTransport, base.Id, PollingInterval, _consoleDebuggingEnabled)
				{
					EnableLogging = this.InternalEnableLogging,
					CustomLogger = this.InternalCustomLogger
                };

                base.DeviceProtocol = _relayProtocol;
                base.DeviceProtocol.Initialize(DriverData);
                _relayProtocol.RxOut += SendRxOut;
				_relayProtocol.ConnectedChanged += ProtocolConnectedChanged;
                _relayProtocol.UserAttributeChanged += ProtocolAttributeChanged;
                _relayProtocol.FeedbackChanged += ProtocolFeedbackChanged;
				

				_relayEmulator = new RelayEmulator();
				_relayEmulator.StateChangedEvent += RelayEmulatorRelayStateChangedEvent;
            }
			catch (Exception ex)
			{
				CrestronConsole.PrintLine("RelayProtocol Error: {0}", ex.Message);

				if (EnableLogging)
				{
					Log(string.Format("RelayProtocol Error: {0}", ex.Message));
				}
			}
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
        private void RelayDriverIpUserAttributesChanged(object sender, UserAttributeListEventArgs e)
        {
			if (_consoleDebuggingEnabled) CrestronConsole.PrintLine($"SetUserAttribute {sender.ToString()} to bool:{e.ToString()}");
		}

        #endregion

        #region IConnection Members

        public override void Connect()
		{
			base.Connect();
            UpdateStateIcons();
			//CrestronConsole.PrintLine("In Connect()");
			Refresh();
        }

		public override void Reconnect()
		{
			base.Reconnect();
            //CrestronConsole.PrintLine("In Reconnect()");
			UpdateStateIcons();
        }


		#endregion

		#region Programmable Operations

		[ProgrammableOperation(OffLabel)]
		public void Off()
		{
			if (EnableLogging)
				Log("Off command triggered from sequence");
			_relayEmulator.Off();
		}

		[ProgrammableOperation(OffWithDelayLabel)]
		public void Off(
			[Display(DelayLabel)]
			[Min(0)]
			[Max(1440)]
			[Unit(Unit.Minutes)]
			int delayTime)
		{
			if (EnableLogging)
				Log($"Off command with {delayTime} minutes delay triggered from sequence");
			_relayEmulator.Off(delayTime);
		}

		[ProgrammableOperation(OnLabel)]
		public void On()
		{
			if (EnableLogging)
				Log("On command triggered from sequence");

			_relayEmulator.On();

		}

		[ProgrammableOperation(OnWithAutoOffLabel)]
		public void On(
			[Display(AutoOffTimeLabel)]
			[Min(0)]
			[Max(1440)]
			[Unit(Unit.Minutes)]
			int autoOffTime)
		{
			if (EnableLogging)
				Log($"On command with {autoOffTime} minutes auto Off triggered from sequence");

            //_relayEmulator.AutoOff = true;
            //_relayEmulator.AutoOffTime = autoOffTime;
            //_relayEmulator.On();
            _relayEmulator.On(true, autoOffTime);
        }

		[ProgrammableOperation(EnableAutoOffLabel)]
		public void EnableAutoOff()
		{
			if (EnableLogging)
				Log("EnableAutoOff command triggered from sequence");

			_relayEmulator.AutoOff = true;
		}

		[ProgrammableOperation(DisableAutoOffLabel)]
		public void DisableAutoOff()
		{
			if (EnableLogging)
				Log("DisableAutoOff command triggered from sequence");

			_relayEmulator.AutoOff = false;
		}

		[ProgrammableOperation(SetAutoOffTimeLabel)]
		public void SetAutoOffTime(
			[Display(AutoOffTimeLabel)]
			[Min(0)]
			[Max(1440)]
			[Unit(Unit.Minutes)]
			int autoOffTime)
		{
			if (EnableLogging)
				Log($"SetAutoOffTime command with {autoOffTime} minute auto off triggered from sequence");

			_relayEmulator.AutoOffTime = autoOffTime;
		}

		#endregion

        #region Programmable Events

        [ProgrammableEvent(TurnedOnLabel)]
        [TriggeredBy(OnLabel)]
        public event EventHandler TurnedOn;

        [ProgrammableEvent(TurnedOffLabel)]
        [TriggeredBy(OffLabel, OffWithDelayLabel)]
        public event EventHandler TurnedOff;

        [ProgrammableEvent(ErrorLabel)]
        public event EventHandler Error;

        #endregion

		#region Private Methods

		private void CreateDeviceDefinition()
		{
			// Define the state property
			_relayStateProperty = CreateProperty<string>(
				new PropertyDefinition(RelayStateKey, RelayStateTranslationKey, DevicePropertyType.String));

			// Define the state icon property
			_relayStateIconProperty = CreateProperty<string>(new PropertyDefinition(RelayStateIconKey, null, DevicePropertyType.String));

			// Define the auto off property
			_autoOffProperty = CreateProperty<bool>(new PropertyDefinition(AutoOffKey, AutoOffTranslationKey, DevicePropertyType.Boolean));

			// Define the auto off time property
			_autoOffTimeProperty = CreateProperty<int>(
				new PropertyDefinition(AutoOffTimeKey, AutoOffTimeTranslationKey, DevicePropertyType.Int32, 1, 1440, 1));
		}

		/// <summary>
		/// Update the state of all properties.
		/// </summary>
		private void Refresh()
        {
            if (_relayEmulator == null)
				return;

            // Refresh settings
			_autoOffProperty.Value = _relayEmulator.AutoOff;
			_autoOffTimeProperty.Value = _relayEmulator.AutoOffTime;

			Commit();
		}

		private object DoCommandThreadCallback(object userSpecific)
		{
			var doCommandObject = (DoCommandObject)userSpecific;
			var command = doCommandObject.Command;
			var parameters = doCommandObject.Parameters;

            switch (command)
			{
				case OnCommand:
                    _relayEmulator.On();
                    break;

				case OffCommand:
                    _relayEmulator.Off();
                    break;

				case ToggleCommand:
					// If the relay is in a transitioning state (ie. turning on) do nothing
					switch (_relayEmulator.RelayState)
					{
						case RelayState.TurnedOff:
							_relayEmulator.On();
                            break;

						case RelayState.TurnedOn:
							_relayEmulator.Off();
                            break;
					}
					break;
			}

			return null;
		}

        private void ProtocolFeedbackChanged(object sender, ValueEventArgs<RelayState> e)
        {
			CrestronConsole.PrintLine($"ProtocolFeedbackChanged Event {e.Value}");
            if (e != null)
            {
                if (e.Value == RelayState.TurnedOn)
                {
                    _relayEmulator.TurnedOn();
                }
				else if (e.Value == RelayState.TurnedOff)
                {
					_relayEmulator.TurnedOff();
                }
            }
        }

		private void RelayEmulatorRelayStateChangedEvent(object sender, StateChangeEventArgs stateChangeEventArgs)
		{
			switch (stateChangeEventArgs.EventType)
			{
				case EventType.RelayStateChanged:
					SetRelayState((RelayState)stateChangeEventArgs.EventData);
					break;

				case EventType.AutoOffChanged:
					_autoOffProperty.Value = (bool)stateChangeEventArgs.EventData;
					break;

				case EventType.AutoOffTimeChanged:
					_autoOffTimeProperty.Value = (int)stateChangeEventArgs.EventData;
					break;
			}

			Commit();
		}

		//process feedback
		private void SetRelayState(RelayState state)
		{
            if (_consoleDebuggingEnabled) CrestronConsole.PrintLine($"SetRelayState {state}");
			switch (state)
			{
				case RelayState.TurnedOn:
					_relayStateProperty.Value = TurnedOnLabel;
					_relayStateIconProperty.Value = _onIcon;
					RaiseTurnedOnEvent();
					break;
				case RelayState.TurningOn:
					_relayStateProperty.Value = TurningOnLabel;
					_relayStateIconProperty.Value = SpinnerIcon;
                    _relayProtocol.SendCustomCommandByName("PowerOn");
					break;
				case RelayState.TurnedOff:
					_relayStateProperty.Value = TurnedOffLabel;
					_relayStateIconProperty.Value = _offIcon;
					RaiseTurnedOffEvent();
					break;
				case RelayState.TurningOff:
					_relayStateProperty.Value = TurningOffLabel;
					_relayStateIconProperty.Value = SpinnerIcon;
                    _relayProtocol.SendCustomCommandByName("PowerOff");
					break;
				case RelayState.Error:
                    _relayStateProperty.Value = ErrorLabel;
                    _relayStateIconProperty.Value = ErrorIcon;
					RaiseErrorEvent();
                    break;
			}
		}
		private void AddUserAttributes()
		{
            AddUserAttribute(
                UserAttributeType.Custom,
                "onIcon",
                "Status On Icon",
                "Enter icon name from available list of extension device icons. Leave empty for default icon",
                true,
                UserAttributeRequiredForConnectionType.None,
                UserAttributeDataType.String,
                "icRemoteButtonGreen");

            AddUserAttribute(
                UserAttributeType.Custom,
                "offIcon",
                "Status Off Icon",
                "Enter icon name from available list of extension device icons. Leave empty for default icon",
                true,
                UserAttributeRequiredForConnectionType.None,
                UserAttributeDataType.String,
                "icRemoteButtonRed");


			UpdateStateIcons();
        }

        private void RaiseTurnedOnEvent()
        {
            var turnedOn = TurnedOn;

            turnedOn?.Invoke(this, new EventArgs());
        }

        private void RaiseTurnedOffEvent()
        {
            var turnedOff = TurnedOff;

            turnedOff?.Invoke(this, new EventArgs());
        }

        private void RaiseErrorEvent()
        {
            var error = Error;

            error?.Invoke(this, new EventArgs());
        }


		private void UpdateStateIcons()
        {
            var userAttributes = RetrieveUserAttributes();

            var onIcon = userAttributes
                .FirstOrDefault(x => x.ParameterId == "onIcon").Data.DefaultValue;
            if (onIcon != "")
            {
                _onIcon = onIcon;
                if (_consoleDebuggingEnabled) CrestronConsole.PrintLine(onIcon);
            }

            var offIcon = userAttributes
                .FirstOrDefault(x => x.ParameterId == "offIcon").Data.DefaultValue;
            if (offIcon != "")
            {
                _offIcon = offIcon;
                if (_consoleDebuggingEnabled) CrestronConsole.PrintLine(offIcon);
            }
		}

		public object GetPropertyValue(object userAttribute, string propertyName)
		{
			return userAttribute.GetType().GetProperties()
				.Single(pi => pi.Name == propertyName)
				.GetValue(userAttribute, null);
		}
		#endregion

        #region IConnection3 Members

		protected virtual void ProtocolConnectedChanged(object driver, ValueEventArgs<bool> e)
		{
            if (_consoleDebuggingEnabled) CrestronConsole.PrintLine("ConnectedChanged Event");
			if (e != null)
				Connected = e.Value;
		}

        protected virtual void ProtocolAttributeChanged(object driver, ValueEventArgs<string[]> e)
        {
            if (_consoleDebuggingEnabled) CrestronConsole.PrintLine($"ProtocolAttributeChanged Event");

            if (e != null)
            {
                var attributeId = e.Value[0];
                var attributeValue = e.Value[1];

                if (attributeId == "onIcon")
                {
                    this._onIcon = attributeValue;
                    CrestronConsole.PrintLine($"onIcon changed to: {attributeValue}");
				}
				else if (attributeId == "offIcon")
                {
                    this._offIcon = attributeValue;
                    CrestronConsole.PrintLine($"offIcon changed to: {attributeValue}");
				}
			}
        }
		#endregion
	}

	internal class DoCommandObject
	{
		public DoCommandObject(string command, string[] parameters)
		{
			Command = command;
			Parameters = parameters;
		}

		public string Command;
		public string[] Parameters;
	}
}