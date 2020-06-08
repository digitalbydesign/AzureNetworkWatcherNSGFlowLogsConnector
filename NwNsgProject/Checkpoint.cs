using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace nsgFunc
{
    using Microsoft.Extensions.Logging;

    public class Checkpoint : TableEntity
    {
        public int CheckpointIndex { get; set; }  // index of the last processed block list item

        public Checkpoint()
        {
        }

        public Checkpoint(string partitionKey, string rowKey, string blockName, long offset, int index)
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
            CheckpointIndex = index;
        }

        public static Checkpoint GetCheckpoint(BlobDetails blobDetails, CloudTable checkpointTable, ILogger log)
        {
            try
            {
                TableOperation operation = TableOperation.Retrieve<Checkpoint>(
                    blobDetails.GetPartitionKey(),
                    blobDetails.GetRowKey());
                TableResult result = checkpointTable.ExecuteAsync(operation).Result;

                Checkpoint checkpoint = (Checkpoint)result.Result;
                if (checkpoint == null)
                {
                    checkpoint = new Checkpoint(blobDetails.GetPartitionKey(), blobDetails.GetRowKey(), "", 0, 1);
                }

                if (checkpoint.CheckpointIndex == 0)
                {
                    checkpoint.CheckpointIndex = 1;
                }

                return checkpoint;
            }
            catch (Exception ex)
            {
                log.LogError(string.Format("Error GetCheckpoint: {0}", ex.Message));
                throw ex;
            }
        }

        public void PutCheckpoint(CloudTable checkpointTable, int index, ILogger log)
        {
            try
            {
                CheckpointIndex = index;

                TableOperation operation = TableOperation.InsertOrReplace(this);
                checkpointTable.ExecuteAsync(operation).Wait();
            }
            catch (Exception ex)
            {
                log.LogError(string.Format("Error PutCheckpoint: {0}", ex.Message));
                throw ex;
            }
        }
    }
}
