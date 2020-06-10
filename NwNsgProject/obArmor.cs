namespace nsgFunc
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public partial class Util
    {
        // ReSharper disable InconsistentNaming
       
        // If global setting for logging is enabled. Log Information for debugging. Will be helpful in investigation.
        private static readonly bool ENABLE_DEBUG_LOG = Convert.ToBoolean(GetEnvironmentVariable("enableDebugLog"));
       
        // ReSharper restore InconsistentNaming

        /// <summary>
        /// The payload that will be sent to Armor for ingestion into the event pipeline
        /// </summary>
        public class ArmorPayload
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ArmorPayload"/> class.
            /// </summary>
            /// <param name="message">The message.</param>
            /// <param name="messageEncoded">IP FIX format encoded string for individual record from `flowTuples`.</param>
            /// <param name="tenantId">The tenant identifier.</param>
            public ArmorPayload(string message, string messageEncoded, int tenantId)
            {
                Message = message;
                MessageEncoded = messageEncoded;
                MessageType = "aws-vpc-flows"; //TODO: Need to add NSG Flow Log type once finalized. Right now routing through vpc log.
                Tags = new[] { "relayed" };
                TenantId = tenantId;
                ExternalId = Guid.Parse(tenantId.ToString("D32")).ToString("D");
            }

            [JsonProperty("tags")]
            public string[] Tags { get; }

            [JsonProperty("type")]
            public string MessageType { get; }

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
            public string MessageEncoded { get; }

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
            // TODO: Figure this out
            // Should this be creating a collection of ipfix packets
            // and sending that as a single event with the original newClientContent as the message?
            // It feels like we should send this all as a single event into the eventing platform
            // and let the processor handle the rest
            foreach (var content in ConvertToArmorPayload(newClientContent, log))
            {
                await obLogstash(content, log).ConfigureAwait(false);
            }
        }

        public static System.Collections.Generic.IEnumerable<string> ConvertToArmorPayload(string newClientContent, ILogger log)
        {
            var tenantId = GetTenantIdFromEnvironment(log);
            foreach (var content in DenormalizedRecord(newClientContent, log))
            {
                var ipFixEncodedLog = ConvertToIpFixFormat(content, log);
                yield return JsonConvert.SerializeObject(new ArmorPayload(content.Message, ipFixEncodedLog, tenantId), Formatting.None);
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
                if (ENABLE_DEBUG_LOG)
                {
                    log.LogInformation(
                        $"Start of IP FIX conversion record: {JsonConvert.SerializeObject(record)}");
                }

                var protocolIdentifier =
                    record.transportProtocol == "U" ? (byte) ProtocolType.Udp : (byte) ProtocolType.Tcp;

                var direction = record.deviceDirection.Equals("I", StringComparison.InvariantCultureIgnoreCase)
                    ? (byte)0 // 0 - ingress flow
                    : (byte) 1; // 1 - egress flow

                // Initialize packets and bytes for FieldType.
                // Default to Input as this is mandatory and if not version 2 then zero is passed for packet and byte.
                var packetsType = NetFlowInformationElement.InputPackets;
                var bytesType = NetFlowInformationElement.InputBytes;

                // Only version above 2 has packets and bytes (input/output). 
                if (record.version >= 2.0 && record.flowState != "B" && record.deviceDirection == "O")
                {
                    // If device direction is Input, assign appropriate FieldType.
                    packetsType = NetFlowInformationElement.OutputPackets;
                    bytesType = NetFlowInformationElement.OutputBytes;
                }

                // Based on direction of device get packets and bytes count.
                var packetDeltaCount = GetPacketCountFromFlowLog(record, log);
                var octetDeltaCount = GetOctetCountFromFlowLog(record, log);

                // Start and End time to be same.
                var flowStartSeconds = Convert.ToUInt32(record.startTime);
                var flowEndSeconds = Convert.ToUInt32(record.startTime);

                // Cisco Netflow v9.
                var templateDef =
                    new TemplateFlow(555)
                        .Field(NetFlowInformationElement.IPV4SourceAddress, 4)
                        .Field(NetFlowInformationElement.L4SourcePort, 2)
                        .Field(NetFlowInformationElement.IPV4DestionationAddress, 4)
                        .Field(NetFlowInformationElement.L4DestionationPort, 2)
                        .Field(NetFlowInformationElement.Protocol, 1)
                        .Field(packetsType, 4)
                        .Field(bytesType, 4)
                        .Field(NetFlowInformationElement.FirstSwitched, 4)
                        .Field(NetFlowInformationElement.LastSwitched, 4)
                        .Field(NetFlowInformationElement.InterfaceName, 50)
                        .Field(NetFlowInformationElement.Direction, 1);


                var templateData =
                    new TemplateData(templateDef)
                        .Data(
                            IPAddress.TryParse(record.sourceAddress, out var sourceAddress)
                                ? sourceAddress
                                : IPAddress.Any,
                            ushort.TryParse(record.sourcePort, out var sourcePort) ? sourcePort : (ushort) 0,
                            IPAddress.TryParse(record.destinationAddress, out var destinationAddress)
                                ? destinationAddress
                                : IPAddress.Any,
                            ushort.TryParse(record.destinationPort, out var destinationPort)
                                ? destinationPort
                                : (ushort) 0,
                            protocolIdentifier,
                            packetDeltaCount,
                            octetDeltaCount,
                            flowStartSeconds,
                            flowEndSeconds,
                            record.mac,
                            direction
                        );

                var exportData =
                    new ExportPacket(0, 1234)
                        .Template(templateData)
                        .GetData(flowStartSeconds);

                var base64Encoded = Convert.ToBase64String(exportData, Base64FormattingOptions.None);

                if (ENABLE_DEBUG_LOG)
                {
                    // https://stackoverflow.com/questions/5666413/ipfix-data-over-udp-to-c-sharp-can-i-decode-the-data
                    log.LogInformation(
                        "End of IP FIX conversion {base64Encoded}", base64Encoded);
                }

                return base64Encoded;
            }
            catch (Exception ex)
            {
                log.LogError(
                    $"Exception occurred in ConvertToIpFixFormat record: {JsonConvert.SerializeObject(record)} and exception: {ex}");
            }

            return string.Empty;

        }

        private static uint GetOctetCountFromFlowLog(DenormalizedRecord record, ILogger log)
        {
            if (!(record.version >= 2.0))
            {
                log.LogWarning("Only version 2 supported {version}", record.version);
                return 0;
            }

            if (record.flowState != "B")
            {
                return record.deviceDirection == "I"
                    ? Convert.ToUInt32(record.bytesStoD)
                    : Convert.ToUInt32(record.bytesDtoS);
            }

            return 0;
        }

        private static uint GetPacketCountFromFlowLog(DenormalizedRecord record, ILogger log)
        {
            if (!(record.version >= 2.0))
            {
                log.LogWarning("Only version 2 supported {version}", record.version);
                return 0;
            }

            if (record.flowState != "B")
            {
                return record.deviceDirection == "I"
                    ? Convert.ToUInt32(record.packetsStoD)
                    : Convert.ToUInt32(record.packetsDtoS);

            }

            return 0;
        }

        static IEnumerable<ArmorDenormalizedRecord> DenormalizedRecord(string newClientContent, ILogger log)
        {
            var logs = JsonConvert.DeserializeObject<NSGFlowLogRecords>(newClientContent);

            foreach (var record in logs.records)
            {
                var version = record.properties.Version;
                var message = JsonConvert.SerializeObject(record, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

                foreach (var outerFlow in record.properties.flows)
                {
                    foreach (var innerFlow in outerFlow.flows)
                    {
                        foreach (var flowTuple in innerFlow.flowTuples)
                        {
                            var tuple = new NSGFlowLogTuple(flowTuple, version);

                            yield return new ArmorDenormalizedRecord(
                                record.properties.Version,
                                record.time,
                                record.category,
                                record.operationName,
                                record.resourceId,
                                outerFlow.rule,
                                innerFlow.mac,
                                tuple,
                                message);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the tenant identifier from the Armor Account Id environment variable.
        /// </summary>
        /// <returns></returns>
        private static int GetTenantIdFromEnvironment(ILogger log)
        {
            var accountIdEnvironmentVariable = Util.GetEnvironmentVariable("armorAccountId");
            if (int.TryParse(accountIdEnvironmentVariable, out var accountId))
            {
                return accountId;
            }

            log.LogError(string.IsNullOrWhiteSpace(accountIdEnvironmentVariable)
                ? "Value for armorAccountId is required."
                : "Value for armorAccountId must be a natural number.");
            throw new System.ArgumentNullException("armorAccountId", "Please provide your Armor Account ID as armorAccountId.");
        }
    }
}
