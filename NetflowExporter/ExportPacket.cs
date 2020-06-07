namespace Armor.NetflowExporter
{
    using System.Collections.Generic;
    using System.Linq;

    public class ExportPacket
    {
        private readonly ushort _sequence;
        private readonly ushort _sourceId;
        private readonly List<TemplateData> _dataFlows = new List<TemplateData>();

        public ExportPacket(ushort sequence, ushort sourceId)
        {
            _sequence = sequence;
            _sourceId = sourceId;
        }

        public ExportPacket Template(TemplateData dataFlow)
        {
            Add(dataFlow);
            return this;
        }

        public void Add(TemplateData dataFlow)
        {
            _dataFlows.Add(dataFlow);
        }

        public void Generate(PacketGenerator packet, uint unixSeconds)
        {
            var count = (ushort)_dataFlows.Sum(x => 1 + x.DataCount);
            packet.AddInt16(9); //Version
            packet.AddInt16(count); //Number of Flowsets

            //packet.AddInt32(DateHelpers.GetUpTimeMS()); //sysUpTime
            //packet.AddInt32(DateHelpers.GetEpoch()); // UNIX Secs

            packet.AddInt32(unixSeconds); //sysUpTime
            packet.AddInt32(unixSeconds); // UNIX Secs

            packet.AddInt32(_sequence); // sequence number
            packet.AddInt32(_sourceId); // source id

            foreach (var dataFlow in _dataFlows)
            {
                dataFlow.Generate(packet);
            }
        }

        public byte[] GetData(uint unixSeconds)
        {
            var packet = new PacketGenerator();
            Generate(packet, unixSeconds);
            return packet.Data;
        }
    }
}
