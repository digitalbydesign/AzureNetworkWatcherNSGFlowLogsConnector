namespace Armor.NetflowExporter
{
    public class FieldDefinition
    {
        private readonly FieldType _fieldId = FieldType.UNKNOWN;
        private readonly ushort _size;
        private readonly ushort _customFieldId = 0;

        public FieldDefinition(FieldType fieldId, ushort size)
        {
            _fieldId = fieldId;
            _size = size;
        }

        public FieldDefinition(ushort customFieldId, ushort size)
        {
            _customFieldId = customFieldId;
            _size = size;
        }

        public ushort Size => _size;

        public void Generate(PacketGenerator packet)
        {
            if (_customFieldId != 0)
            {
                packet.AddInt16(_customFieldId);
            }
            else
            {
                packet.AddInt16((ushort)_fieldId);
            }
            packet.AddInt16(_size);

        }
    }
}
