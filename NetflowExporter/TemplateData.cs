namespace Armor.NetflowExporter
{
    using System.Collections.Generic;

    public class TemplateData
    {
        private readonly TemplateFlow _template;
        private readonly List<DataFlow> _data = new List<DataFlow>();

        public TemplateData(TemplateFlow template)
        {
            _template = template;
        }
        
        public ushort DataCount => (ushort)_data.Count;

        public void AddData(params object[] values)
        {
            _data.Add(new DataFlow(_template, values));
        }

        public TemplateData Data(params object[] values)
        {
            AddData(values);
            return this;
        }

        public void Generate(PacketGenerator packet)
        {
            _template.Generate(packet);
            foreach (var data in _data)
            {
                data.Generate(packet);
            }
        }
    }

}
