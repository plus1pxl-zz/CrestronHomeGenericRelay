

namespace RelayGenericIP
{
    using System;
    using System.Collections.Generic;
    using Crestron.RAD.Common;
    using Crestron.RAD.Common.BasicDriver;
    using Crestron.RAD.Common.Enums;
    using Crestron.RAD.Common.Events;
    using Crestron.RAD.Common.Transports;
    using Crestron.SimplSharp;
    using Crestron.RAD.Common.Interfaces;

    public class RelayProtocol : ABaseDriverProtocol, IDisposable
    {
        #region Fields

        private bool _isConnected;
        private bool _consoleDebuggingEnabled;
        private RelayState _relayState;

        #endregion

        #region Events

        public event EventHandler<ValueEventArgs<bool>> ConnectedChanged;
        public event EventHandler<ValueEventArgs<string[]>> UserAttributeChanged;
        public event EventHandler<ValueEventArgs<RelayState>> FeedbackChanged;

        #endregion

        #region Constructor

        public RelayProtocol(ISerialTransport transport, byte id)
            : base(transport, id)
        {
        }

        public RelayProtocol(ISerialTransport transport, byte id, int pollingInterval)
            : base(transport, id)
        {
            base.PollingInterval = pollingInterval;
        }
        public RelayProtocol(ISerialTransport transport, byte id, int pollingInterval, bool consoleDebuggingEnabled)
            : base(transport, id)
        {
            base.PollingInterval = pollingInterval;
            this._consoleDebuggingEnabled = consoleDebuggingEnabled;
        }
        #endregion

        #region Property

        public Dictionary<StandardCommandsEnum, string> CommandsDictionary
        {
            get;
            protected internal set;
        }

        #endregion

        protected override void ConnectionChangedEvent(bool connection)
        {
            if (ConnectedChanged != null)
                ConnectedChanged(null, new ValueEventArgs<bool>(connection));
        }

        protected override void MessageTimedOut(string lastSentCommand)
        {
            LogMessage("RelayProtocol, MessageTimedOut: " + lastSentCommand);
        }

        protected override void ConnectionChanged(bool connection)
        {
            if (connection == _isConnected) return;
            _isConnected = connection;
            base.ConnectionChanged(connection);
            if (connection) Poll();
            // debugging
            if (_consoleDebuggingEnabled) CrestronConsole.PrintLine($"_isConnected field is set to:{_isConnected}");
        }

        protected override void Poll()
        {
            this.SendCustomCommandByName("PowerPoll");
        }

        protected override bool PrepareStringThenSend(CommandSet commandSet)
        {
            if (!commandSet.CommandPrepared)
            {
                commandSet.Command = $"{commandSet.Command}\x0D";
                commandSet.CommandPrepared = true;
            }
            return base.PrepareStringThenSend(commandSet);
        }

        protected override void ChooseDeconstructMethod(ValidatedRxData validatedData)
        {
        }

        public override void DataHandler(string rx)
        {

            if (rx.ToLower().Contains("is on")) // && _relayState != RelayState.TurnedOn
            {
                _relayState = RelayState.TurnedOn;
            }
            else if (rx.ToLower().Contains("is off")) //  && _relayState != RelayState.TurnedOff
            {
                _relayState = RelayState.TurnedOff;
            }
            else if (rx.ToLower().Contains("error"))
            {
                _relayState = RelayState.Error;
            }

            FeedbackChanged?.Invoke(this, new ValueEventArgs<RelayState>(_relayState));
        }

        public override void SetUserAttribute(string attributeId, string attributeValue)
        {
            if (_consoleDebuggingEnabled) CrestronConsole.PrintLine($"SetUserAttribute {attributeId} to string:{attributeValue}");
            if (attributeId != null)
            {
                if (attributeId == "onIcon" || attributeId == "offIcon")
                {
                    
                    string[] attributeData = { attributeId, attributeValue};
                    if (UserAttributeChanged != null)
                        this.UserAttributeChanged.Invoke(this, new ValueEventArgs<string[]>(attributeData));
                }
            }
        }


        public override void SetUserAttribute(string attributeId, ushort attributeValue)
        {
            if (_consoleDebuggingEnabled) CrestronConsole.PrintLine($"SetUserAttribute {attributeId} to ushort:{attributeValue}");
        }

        public override void SetUserAttribute(string attributeId, bool attributeValue)
        {
            if (_consoleDebuggingEnabled) CrestronConsole.PrintLine($"SetUserAttribute {attributeId} to bool:{attributeValue}");
        }

        #region Logging

        internal void LogMessage(string message)
        {
            if (!EnableLogging) return;

            if (CustomLogger == null)
            {
                CrestronConsole.PrintLine(message);
            }
            else
            {
                CustomLogger(message + "\n");
            }
        }

        public List<UserAttribute> RetrieveUserAttributes()
        {
            throw new NotImplementedException();
        }

        #endregion Logging
    }
}
