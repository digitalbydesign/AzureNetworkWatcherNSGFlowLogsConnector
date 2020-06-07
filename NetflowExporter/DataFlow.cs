namespace Armor.NetflowExporter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class DataFlow
    {
        private readonly TemplateFlow _template;
        private readonly List<byte[]> _dataValues = new List<byte[]>();
        private readonly ushort _dataLength = 0;
        private readonly ushort _paddingLength = 0;

        public DataFlow(TemplateFlow template, params object[] values)
        {
            _template = template;
            var values1 = values.ToList();

            if (values1.Count != template.FieldCount)
                throw new ArgumentException(
                    $"Incorrect number of data values provided. Expected {_template.FieldCount}. Provided {values1.Count}.");

            for (var i = 0; i < values1.Count; i++)
            {
                var data = BitConverterEx.ToBytes(_template[i], values1[i]);
                _dataValues.Add(data);
                _dataLength = (ushort)(_dataLength + data.Length);
            }

            if (_dataLength % 4 != 0)
            {
                _paddingLength = (ushort)(4 - _dataLength % 4);
                _dataLength = (ushort)(_dataLength + _paddingLength);
            }
        }

        public void Generate(PacketGenerator packet)
        {
            packet.AddInt16(_template.ID);                  // FlowsetID
            packet.AddInt16((ushort)(2 + 2 + _dataLength)); // Length

            foreach (var data in _dataValues)
            {
                packet.Add(data);
            }

            if (_paddingLength != 0)
            {
                var padding = new byte[_paddingLength];
                packet.Add(padding);
            }
        }
    }

}
