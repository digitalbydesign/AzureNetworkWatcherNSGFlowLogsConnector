namespace Armor.NetflowExporter
{
    using System.Collections.Generic;
    using System.Linq;

    public class TemplateFlow
    {
        private readonly ushort _id;
        private readonly List<FieldDefinition> _fields;

        public TemplateFlow(ushort id)
        {
            _id = id;
            _fields = new List<FieldDefinition>();
        }

        public TemplateFlow(ushort id, params FieldDefinition[] fields)
        {
            _id = id;
            _fields = fields.ToList();
        }

        public TemplateFlow Field(FieldType fieldId, ushort size)
        {
            var field = new FieldDefinition(fieldId, size);
            _fields.Add(field);
            return this;
        }

        public ushort ID => _id;

        public ushort FieldCount => (ushort)_fields.Count;

        public FieldDefinition this[int index] => _fields[index];

        public void Generate(PacketGenerator packet)
        {
            ushort length = (ushort)(2 + 2 + 2 + 2 + FieldCount * (2 + 2));

            packet.AddInt16(0);           // FlowsetID
            packet.AddInt16(length);      // Length
            packet.AddInt16(_id);         // Template ID
            packet.AddInt16(FieldCount);  // FieldCount

            foreach (var field in _fields)
            {
                field.Generate(packet);
            }
        }
    }
}
