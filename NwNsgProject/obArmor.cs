namespace nsgFunc
{
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System.Text;
    using System.Threading.Tasks;
    using System;
    using System.Net;
    using System.Net.Sockets;
    using NwNsgProject;

    public partial class Util
    {
        /// <summary>
        /// The payload that will be sent to Armor for ingestion into the event pipeline
        /// </summary>
        public class ArmorPayload
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ArmorPayload"/> class.
            /// </summary>
            /// <param name="message">The message.</param>
            /// <param name="payload">The payload.</param>
            /// <param name="tenantId">The tenant identifier.</param>
            /// <param name="ipFixEncodedLog">IPFIX format encoded string for individual record from `flowTuples`.</param>
            public ArmorPayload(string message, string payload, int tenantId, string ipFixEncodedLog)
            {
                Message = message;
                IpFixEncodedFlowLog = ipFixEncodedLog;
                Payload = payload;
                TenantId = tenantId;
                ExternalId = System.Guid.Parse(tenantId.ToString("D32")).ToString("D");
            }

            /// <summary>
            /// Gets the external identifier.
            /// </summary>
            /// <value>
            /// The external identifier for an event that is a guid representation of an integer that represents the Customer AccountID.
            /// </value>
            [JsonProperty("external_id")]
            public string ExternalId { get; }

            /// <summary>
            /// Gets the message.
            /// </summary>
            /// <value>
            /// The message.
            /// </value>
            [JsonProperty("message")]
            public string Message { get; }

            /// <summary>
            /// Gets IPFIX converted format for individual record from `flowTuples`
            /// </summary>
            /// <value>
            /// The message.
            /// </value>
            [JsonProperty("message_encoded")]
            public string IpFixEncodedFlowLog { get; }

            /// <summary>
            /// Gets the payload.
            /// </summary>
            /// <value>
            /// The payload.
            /// </value>
            [JsonProperty("payload")]
            public string Payload { get; }

            /// <summary>
            /// Gets or sets the tenant identifier.
            /// </summary>
            /// <value>
            /// The tenant identifier.
            /// </value>
            [JsonProperty("tenant_id")]
            public int TenantId { get; set; }
        }

        /// <summary>
        /// Output Binding to Armor.
        /// </summary>
        /// <param name="newClientContent">New content of the client.</param>
        /// <param name="log">The log.</param>
        /// <returns></returns>
        
        public static async Task ObArmor(string newClientContent, ILogger log)
        {
            foreach (var content in ConvertToArmorPayload(newClientContent, log))
            {
                await obLogstash(content, log).ConfigureAwait(false);
            }
        }

        static System.Collections.Generic.IEnumerable<string> ConvertToArmorPayload(string newClientContent, ILogger log)
        {
            var tenantId = GetTenantIdFromEnvironment(log);
            foreach (var content in DenormalizedRecord(newClientContent, log))
            {
                var outgoingRecord = content.FirstOrDefault();
                var outgoingJson = JsonConvert.SerializeObject(outgoingRecord, new JsonSerializerSettings
                                                                                   {
                                                                                       NullValueHandling = NullValueHandling.Ignore
                                                                                   });

                var ipFixEncodedLog = ConvertToIpFixFormat(outgoingRecord, log);

                yield return JsonConvert.SerializeObject(new ArmorPayload(outgoingRecord.Message, outgoingJson, tenantId, ipFixEncodedLog), Formatting.None);
            }
        }

        /// <summary>
        /// Convert to IPFIX format
        /// </summary>
        /// <param name="record">Each NSG Flow log tuple.</param>
        /// <param name="log">ILogger for logging.</param>
        /// <returns>Base64 encoded string of byte array.</returns>
        private static string ConvertToIpFixFormat(DenormalizedRecord record, ILogger log)
        {
            try
            {
                // If global setting for logging is enabled. Log Information for debugging. Will be helpful in investigation.
                var isLoggingEnabled = Convert.ToBoolean(GetEnvironmentVariable("logIncomingJSON"));


                if (isLoggingEnabled)
                {
                    log.LogDebug(
                        "Start of IPFIX conversion {record}", record);
                }

                var templateDef =
                        new TemplateFlow(555)
                            .Field(NetFlowInformationElement.SourceIPv4Address, 4)
                            .Field(NetFlowInformationElement.SourceTransportPort, 2)
                            .Field(NetFlowInformationElement.DestinationIPv4Address, 4)
                            .Field(NetFlowInformationElement.DestinationTransportPort, 2)
                            .Field(NetFlowInformationElement.ProtocolIdentifier, 1)
                            .Field(NetFlowInformationElement.PacketDeltaCount, 8)
                            .Field(NetFlowInformationElement.OctetDeltaCount, 8)
                            .Field(NetFlowInformationElement.FlowStartSeconds, 4)
                            .Field(NetFlowInformationElement.FlowEndSeconds, 4)
                            .Field(NetFlowInformationElement.InterfaceName, 65535)
                    ;

                var protocolIdentifier = record.transportProtocol == "U" ? ProtocolType.Udp : ProtocolType.Tcp;

                // TODO: Need to check how to get count of below since there is source destination values.
                // Right now getting count based on device direction.
                var packetDeltaCount = GetPacketCountFromFlowLog(record, log); 
                var octetDeltaCount = GetOctetCountFromFlowLog(record, log);

                // TODO: Only startTime in tuple which is in Unix time format (UTC). What would be end seconds?
                var flowStartSeconds = Convert.ToInt32(record.startTime);
                var flowEndSeconds = Convert.ToInt32(record.startTime);

                var templateData =
                    new DataFlow(templateDef,
                        IPAddress.Parse(record.sourceAddress),
                        Convert.ToUInt16(record.sourcePort),
                        IPAddress.Parse(record.destinationAddress),
                        Convert.ToUInt16(record.destinationPort),
                        Convert.ToByte(protocolIdentifier),
                        Convert.ToUInt32(packetDeltaCount),
                        Convert.ToUInt32(octetDeltaCount),
                        Convert.ToUInt32(flowStartSeconds),
                        Convert.ToUInt32(flowEndSeconds),
                        record.mac
                    );

                var packet = new PacketEncoder();
                templateData.Generate(packet);
                var exportData = packet.Data;

                var base64Encoded = Convert.ToBase64String(exportData, Base64FormattingOptions.None);

                if (isLoggingEnabled)
                {
                    // https://stackoverflow.com/questions/5666413/ipfix-data-over-udp-to-c-sharp-can-i-decode-the-data
                    log.LogDebug(
                        "End of IPFIX conversion {base64Encoded}", base64Encoded);
                }

                return base64Encoded;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Exception occurred in ConvertToIpFixFormat {record}", record);
            }

            return string.Empty;

        }
        
        private static int GetOctetCountFromFlowLog(DenormalizedRecord record, ILogger log)
        {
            if (!(record.version >= 2.0))
            {
                log.LogWarning("Only version 2 supported {version}", record.version);
                return 0;
            }

            if (record.flowState != "B")
            {
                return record.deviceDirection == "I"
                    ? Convert.ToInt32(record.bytesStoD)
                    : Convert.ToInt32(record.bytesDtoS);
            }

            return 0;
        }

        private static int GetPacketCountFromFlowLog(DenormalizedRecord record, ILogger log)
        {
            if (!(record.version >= 2.0))
            {
                log.LogWarning("Only version 2 supported {version}", record.version);
                return 0;
            }

            if (record.flowState != "B")
            {
                return record.deviceDirection == "I"
                    ? Convert.ToInt32(record.packetsStoD)
                    : Convert.ToInt32(record.packetsDtoS);

            }

            return 0;
        }

        static IEnumerable<List<ArmorDenormalizedRecord>> DenormalizedRecord(string newClientContent, ILogger log)
        {
            NSGFlowLogRecords logs = JsonConvert.DeserializeObject<NSGFlowLogRecords>(newClientContent);

            foreach (var record in logs.records)
            {
                float version = record.properties.Version;
                var outgoingList = new List<ArmorDenormalizedRecord>();

                foreach (var outerFlow in record.properties.flows)
                {
                    foreach (var innerFlow in outerFlow.flows)
                    {
                        foreach (var flowTuple in innerFlow.flowTuples)
                        {
                            var tuple = new NSGFlowLogTuple(flowTuple, version);

                            var denormalizedRecord = new ArmorDenormalizedRecord(
                                record.properties.Version,
                                record.time,
                                record.category,
                                record.operationName,
                                record.resourceId,
                                outerFlow.rule,
                                innerFlow.mac,
                                tuple);
                            outgoingList.Add(denormalizedRecord);
                            denormalizedRecord.Message = JsonConvert.SerializeObject(record, new JsonSerializerSettings
                                                                                                 {
                                                                                                     NullValueHandling = NullValueHandling.Ignore
                                                                                                 });
                            yield return outgoingList;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the tenant identifier from the Armor Account Id environment variable.
        /// </summary>
        /// <returns></returns>
        private static int GetTenantIdFromEnvironment( ILogger log)
        {
            var accountId = Util.GetEnvironmentVariable("armorAccountId");
            if (accountId.Length == 0)
            {
                log.LogError("Values for armorAccountId is required.");
                throw new System.ArgumentNullException("armorAccountId", "Please provide armorAccountId.");
            }

            return int.Parse(accountId);
        }
    }
}
