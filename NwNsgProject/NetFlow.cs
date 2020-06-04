namespace NwNsgProject
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Net;

    public class TemplateFlow
    {
        private readonly List<NetFlowElement> _elements;

        public TemplateFlow(ushort id)
        {
            Id = id;
            _elements = new List<NetFlowElement>();
        }

        public TemplateFlow(ushort id, params NetFlowElement[] elements)
        {
            Id = id;
            _elements = elements.ToList();
        }

        public TemplateFlow Field(NetFlowInformationElement fieldId, ushort size)
        {
            var field = new NetFlowElement(fieldId, size);
            _elements.Add(field);
            return this;
        }

        public ushort Id { get; }

        public ushort FieldCount => (ushort)_elements.Count;

        public NetFlowElement this[int index] => _elements[index];

        public void Generate(PacketEncoder packet)
        {
            var length = (ushort)(2 + 2 + 2 + 2 + FieldCount * (2 + 2));

            // Template FlowSet Id
            packet.AddInt16(0);

            // Length
            packet.AddInt16(length);

            // Template ID
            packet.AddInt16(Id);

            // FieldCount
            packet.AddInt16(FieldCount);

            foreach (var field in _elements)
            {
                field.EncodeTo(packet);
            }
        }
    }

    public class DataFlow
    {
        private readonly TemplateFlow _template;
        private readonly List<byte[]> _dataValues = new List<byte[]>();
        private readonly ushort _dataLength;
        private readonly ushort _paddingLength;

        public DataFlow(TemplateFlow template, params object[] values)
        {
            _template = template;

            if (values.Length != template.FieldCount)
                throw new ArgumentException(
                    $"Count of {nameof(_template.FieldCount)} must match count of {nameof(values)}. Expected {nameof(_template.FieldCount)}:{_template.FieldCount}. Provided {nameof(values)}:{values.Length}.");

            for (var i = 0; i < values.Length; i++)
            {
                var data = ToBytes(_template[i], values[i]);
                _dataValues.Add(data);
                _dataLength = (ushort)(_dataLength + data.Length);
            }

            if (_dataLength % 4 == 0) return;

            _paddingLength = (ushort)(4 - _dataLength % 4);
            _dataLength = (ushort)(_dataLength + _paddingLength);
        }

        public void Generate(PacketEncoder packet)
        {
            // FlowSet ID
            packet.AddInt16(_template.Id);

            // Length
            packet.AddInt16((ushort)(2 + 2 + _dataLength));

            foreach (var data in _dataValues)
            {
                packet.Add(data);
            }

            if (_paddingLength == 0) return;
            packet.Add(new byte[_paddingLength]);
        }

        public byte[] ToBytes(NetFlowElement element, object value)
        {
            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.UInt32:
                    return BitConverter.GetBytes((uint) value).Reverse().ToArray();
                case TypeCode.UInt16:
                    return BitConverter.GetBytes((ushort) value).Reverse().ToArray();
                case TypeCode.Byte:
                    return new[] {(byte) value};
                case TypeCode.String when ((string) value).Length > element.Size:
                    throw new ArgumentException($"Invalid length of string:{((string)value).Length} expected:{element.Size}");
                case TypeCode.String:
                {
                    return ((string) value).ToCharArray().Select(x => (byte)x).ToArray();
                }
                case TypeCode.Object when value is IPAddress address:
                    return address.GetAddressBytes();
                default:
                    throw new ArgumentException("Invalid value provided.");
            }
        }
    }

    public class NetFlowElement
    {
        private readonly NetFlowInformationElement _elementId;

        public NetFlowElement(NetFlowInformationElement elementId, ushort size)
        {
            _elementId = elementId;
            Size = size;
        }

        public ushort Size { get; }

        public void EncodeTo(PacketEncoder packet)
        {
            packet.AddInt16((ushort)_elementId);
            packet.AddInt16(Size);

        }
    }

    /// <summary>
    /// <see href="https://www.iana.org/assignments/ipfix/ipfix.xhtml"/>
    /// <seealso href="https://tools.ietf.org/html/rfc5102"/>
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    public enum NetFlowInformationElement : ushort
    {
        Unknown = 0,
        OctetDeltaCount = 1,
        PacketDeltaCount = 2,
        ProtocolIdentifier = 4,
        SourceTransportPort = 7,
        SourceIPv4Address = 8,
        DestinationTransportPort = 11,
        DestinationIPv4Address = 12,
        InterfaceName = 82,
        FlowStartSeconds = 150,
        FlowEndSeconds = 151
    }

    public class PacketEncoder
    {
        private byte[] _data = new byte[0];

        public byte[] Data => _data;

        public void AddInt32(uint val)
        {
            Add(BitConverter.GetBytes(val).Reverse().ToArray());
        }

        public void AddInt16(ushort val)
        {
            Add(BitConverter.GetBytes(val).Reverse().ToArray());
        }

        public void AddIPv4(string ipStr)
        {
            Add(IPAddress.Parse(ipStr).GetAddressBytes());
        }

        public void Add(byte[] data)
        {
            var index = _data.Length;
            Array.Resize(ref _data, _data.Length + data.Length);
            Array.Copy(data, 0, _data, index, data.Length);
        }
    }

    public static class Util
    {
        public static uint ToUnixTime(this DateTime dateTime)
        {
            return (uint)(dateTime - new DateTime(1970, 1, 1)).TotalSeconds;
        }
    }
}
