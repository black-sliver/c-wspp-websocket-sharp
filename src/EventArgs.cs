using System;

namespace WebSocketSharp
{
    public class MessageEventArgs : EventArgs
    {
        private byte[] _data;
        private OpCode _opcode;
        private string _str;

        internal MessageEventArgs(byte[] data, OpCode opcode)
        {
            _data = data;
            _opcode = opcode;
            _str = null;
        }

        public bool IsBinary { get { return _opcode == OpCode.Binary; } }
        public bool IsPing { get { return _opcode == OpCode.Ping; } }
        public bool IsText { get { return _opcode == OpCode.Text; } }
        public byte[] RawData { get { return _data; } }
        public string Data {
            get {
                if (_str == null) {
                    _str = System.Text.Encoding.UTF8.GetString(_data);
                }
                return _str;
            }
        }
    }

    public class CloseEventArgs : EventArgs
    {
        private ushort _code;
        private string _reason;

        internal CloseEventArgs(ushort code, string reason)
        {
            _code = code;
            _reason = reason;
        }

        public ushort Code { get { return _code; } }
        public string Reason { get { return _reason; } }
        public bool WasClean { get { return _code >= 1000 && _code != 1005; } }
    }

    public class ErrorEventArgs : EventArgs
    {
        private string _message;
        private Exception _exception;

        internal ErrorEventArgs(string message, Exception exception)
        {
            _message = message;
            _exception = exception;
        }

        public string Message { get { return _message; } }
        public Exception Exception { get { return _exception; } }
    }
}
