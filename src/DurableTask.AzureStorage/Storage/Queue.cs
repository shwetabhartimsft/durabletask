﻿//  ----------------------------------------------------------------------------------
//  Copyright Microsoft Corporation
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ----------------------------------------------------------------------------------

namespace DurableTask.AzureStorage.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using DurableTask.AzureStorage.Monitoring;
    using Microsoft.WindowsAzure.Storage.Queue;

    class Queue
    {
        readonly AzureStorageClient azureStorageClient;
        readonly CloudQueueClient queueClient;
        readonly AzureStorageOrchestrationServiceStats stats;
        readonly CloudQueue cloudQueue;
        readonly QueueRequestOptions queueRequestOptions;

        public string Name { get; }
        public Uri Uri { get; }
        public int? ApproximateMessageCount => this.cloudQueue.ApproximateMessageCount;

        public Queue(AzureStorageClient azureStorageClient, CloudQueueClient queueClient, string queueName, QueueRequestOptions queueRequestOptions)
        {
            this.azureStorageClient = azureStorageClient;
            this.queueClient = queueClient;
            this.stats = this.azureStorageClient.Stats;
            this.Name = queueName;
            this.queueRequestOptions = queueRequestOptions;

            this.cloudQueue = this.queueClient.GetQueueReference(this.Name);
        }

        public async Task AddMessageAsync(QueueMessage queueMessage, TimeSpan? visibilityDelay, string clientRequestId = null)
        {
            await this.azureStorageClient.MakeStorageRequest(
                (context, cancellationToken) => this.cloudQueue.AddMessageAsync(
                    queueMessage.CloudQueueMessage,
                    null /* timeToLive */,
                    visibilityDelay,
                    this.queueRequestOptions,
                    context),
                "Queue AddMessage",
                clientRequestId);

            this.stats.MessagesSent.Increment();
        }

        public async Task UpdateMessageAsync(QueueMessage queueMessage, TimeSpan visibilityTimeout, string clientRequestId = null)
        {
            await this.azureStorageClient.MakeStorageRequest(
                (context, cancellationToken) => this.cloudQueue.UpdateMessageAsync(
                    queueMessage.CloudQueueMessage,
                    visibilityTimeout,
                    MessageUpdateFields.Visibility,
                    this.queueRequestOptions,
                    context),
                "Queue UpdateMessage",
                clientRequestId);

            this.stats.MessagesUpdated.Increment();
        }

        public async Task DeleteMessageAsync(QueueMessage queueMessage, string clientRequestId = null)
        {
            await this.azureStorageClient.MakeStorageRequest(
                (context, cancellationToken) => this.cloudQueue.DeleteMessageAsync(
                    queueMessage.CloudQueueMessage,
                    this.queueRequestOptions,
                    context),
                "Queue DeleteMessage",
                clientRequestId);
        }

        public async Task<QueueMessage> GetMessageAsync(TimeSpan visibilityTimeout, CancellationToken token)
        {
            var cloudQueueMessage = await this.azureStorageClient.MakeStorageRequest<CloudQueueMessage>(
                (context, cancellationToken) =>
                {
                    using (var finalLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, cancellationToken))
                    {
                        return this.cloudQueue.GetMessageAsync(
                            visibilityTimeout,
                            this.queueRequestOptions,
                            context,
                            finalLinkedCts.Token);
                    }
                },
                "Queue GetMessage");

            if (cloudQueueMessage == null)
            {
                return null;
            }

            this.stats.MessagesRead.Increment();
            return new QueueMessage(cloudQueueMessage);
        }

        public async Task<bool> ExistsAsync()
        {
            return await this.azureStorageClient.MakeStorageRequest<bool>(
                (context, cancellationToken) => this.cloudQueue.ExistsAsync(this.queueRequestOptions, context, cancellationToken),
                "Queue Exists");
        }

        public async Task<bool> CreateIfNotExistsAsync()
        {
            return await this.azureStorageClient.MakeStorageRequest<bool>(
                (context, cancellationToken) => this.cloudQueue.CreateIfNotExistsAsync(this.queueRequestOptions, context, cancellationToken),
                "Queue Create");
        }

        public async Task<bool> DeleteIfExistsAsync()
        {
            return await this.azureStorageClient.MakeStorageRequest<bool>(
                (context, cancellationToken) => this.cloudQueue.DeleteIfExistsAsync(this.queueRequestOptions, context, cancellationToken),
                "Queue Delete");
        }

        public async Task<IEnumerable<QueueMessage>> GetMessagesAsync(int batchSize, TimeSpan visibilityTimeout, CancellationToken token)
        {
            var cloudQueueMessages = await this.azureStorageClient.MakeStorageRequest<IEnumerable<CloudQueueMessage>>(
                (context, cancellationToken) =>
                {
                    using (var finalLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, cancellationToken))
                    {
                        return this.cloudQueue.GetMessagesAsync(
                            batchSize,
                            visibilityTimeout,
                            this.queueRequestOptions,
                            context,
                            finalLinkedCts.Token);
                    }
                },
                "Queue GetMessages");

            var queueMessages = new List<QueueMessage>();
            foreach (CloudQueueMessage cloudQueueMessage in cloudQueueMessages)
            {
                queueMessages.Add(new QueueMessage(cloudQueueMessage));
                this.stats.MessagesRead.Increment();
            }

            return queueMessages;
        }

        public async Task FetchAttributesAsync()
        {
            await this.azureStorageClient.MakeStorageRequest(
                (context, cancellationToken) => this.cloudQueue.FetchAttributesAsync(this.queueRequestOptions, context, cancellationToken),
                "Queue FetchAttributes");
        }

        public async Task<IEnumerable<QueueMessage>> PeekMessagesAsync(int batchSize)
        {
            var cloudQueueMessages = await this.azureStorageClient.MakeStorageRequest<IEnumerable<CloudQueueMessage>>(
                (context, cancellationToken) => this.cloudQueue.PeekMessagesAsync(batchSize, this.queueRequestOptions, context, cancellationToken),
                "Queue PeekMessages");

            var queueMessages = new List<QueueMessage>();
            foreach (CloudQueueMessage cloudQueueMessage in cloudQueueMessages)
            {
                queueMessages.Add(new QueueMessage(cloudQueueMessage));
                this.stats.MessagesRead.Increment();
            }

            return queueMessages;
        }

        public async Task<QueueMessage> PeekMessageAsync()
        {
            var queueMessage = await this.cloudQueue.PeekMessageAsync();
            return queueMessage == null ? null : new QueueMessage(queueMessage);
        }
    }
}