namespace nsgFunc
{
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System.Text;
    using System.Threading.Tasks;

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
            public ArmorPayload(string message, string payload, int tenantId)
            {
                Message = message;
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
        public static Task ObArmor(string newClientContent, ILogger log)
        {
            //var payload = ConvertToArmorPayload(newClientContent, log);
            var payload = denormalizedRecord(newClientContent, log);
            return obLogstash(payload, log);
        }

        /// <summary>
        /// Converts to Armor payload.
        /// </summary>
        /// <param name="newClientContent">New content of the client.</param>
        /// <param name="log">The log.</param>
        /// <returns></returns>
        public static string ConvertToArmorPayload(string content, ILogger log)
        {
            var tenantId = GetTenantIdFromEnvironment(log);

            var payload = new StringBuilder();
           
            payload.AppendLine(content);
           

            return JsonConvert.SerializeObject(new ArmorPayload(content, payload.ToString(), tenantId), Formatting.None);
        }

        static string denormalizedRecord(string newClientContent, ILogger log)
        {
            NSGFlowLogRecords logs = JsonConvert.DeserializeObject<NSGFlowLogRecords>(newClientContent);

            foreach (var record in logs.records)
            {
                float version = record.properties.Version;

                foreach (var outerFlow in record.properties.flows)
                {
                    foreach (var innerFlow in outerFlow.flows)
                    {
                        foreach (var flowTuple in innerFlow.flowTuples)
                        {
                            var tuple = new NSGFlowLogTuple(flowTuple, version);

                            var denormalizedRecord = new DenormalizedRecord(
                                record.properties.Version,
                                record.time,
                                record.category,
                                record.operationName,
                                record.resourceId,
                                outerFlow.rule,
                                innerFlow.mac,
                                tuple);

                            var outgoingJson = JsonConvert.SerializeObject(
                                denormalizedRecord,
                                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

                            return ConvertToArmorPayload(outgoingJson, log);
                        }
                    }
                }
            }

            return string.Empty;
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
