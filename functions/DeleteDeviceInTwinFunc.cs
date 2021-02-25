// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Azure;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace Samples.AdtIothub
{
    public static class DeleteDeviceInTwinFunc
    {
        private static string adtAppId = "https://digitaltwins.azure.net";
        private static readonly string adtInstanceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL", EnvironmentVariableTarget.Process);
        private static readonly HttpClient singletonHttpClientInstance = new HttpClient();

        [FunctionName("DeleteDeviceInTwinFunc")]
        public static async Task Run(
            [EventHubTrigger("lifecycleevents", Connection = "EVENTHUB_CONNECTIONSTRING")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>(events.Length);

            // Create Digital Twin client
            var cred = new ManagedIdentityCredential(adtAppId);
            var client = new DigitalTwinsClient(
                new Uri(adtInstanceUrl),
                cred,
                new DigitalTwinsClientOptions
                {
                    Transport = new HttpClientTransport(singletonHttpClientInstance)
                });

            foreach (EventData eventData in events)
            {
                try
                {
                    //log.LogDebug($"EventData: {System.Text.Json.JsonSerializer.Serialize(eventData)}");

                    string opType = eventData.Properties["opType"] as string;
                    if (opType == "deleteDeviceIdentity")
                    {
                        string deviceId = eventData.Properties["deviceId"] as string;

                        try
                        {
                            // Find twin based on the original Registration ID
                            BasicDigitalTwin digitalTwin = await client.GetDigitalTwinAsync<BasicDigitalTwin>(deviceId);

                            // In order to delete the twin, all relationships must first be removed
                            await DeleteAllRelationshipsAsync(client, digitalTwin.Id, log);

                            // Delete the twin
                            await client.DeleteDigitalTwinAsync(digitalTwin.Id, digitalTwin.ETag);
                            log.LogInformation($"Twin {digitalTwin.Id} deleted in DT");
                        }
                        catch (RequestFailedException e) when (e.Status == (int)HttpStatusCode.NotFound)
                        {
                            log.LogWarning($"Twin {deviceId} not found in DT");
                        }
                    }
                }
                catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    exceptions.Add(e);
                }
            }

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }

        /// <summary>
        /// Deletes all outgoing and incoming relationships from a specified digital twin
        /// </summary>
        public static async Task DeleteAllRelationshipsAsync(DigitalTwinsClient client, string dtId, ILogger log)
        {
            AsyncPageable<BasicRelationship> relationships = client.GetRelationshipsAsync<BasicRelationship>(dtId);
            await foreach (BasicRelationship relationship in relationships)
            {
                await client.DeleteRelationshipAsync(dtId, relationship.Id, relationship.ETag);
                log.LogInformation($"Twin {dtId} relationship {relationship.Id} deleted in DT");
            }

            AsyncPageable<IncomingRelationship> incomingRelationships = client.GetIncomingRelationshipsAsync(dtId);
            await foreach (IncomingRelationship incomingRelationship in incomingRelationships)
            {
                await client.DeleteRelationshipAsync(incomingRelationship.SourceId, incomingRelationship.RelationshipId);
                log.LogInformation($"Twin {dtId} incoming relationship {incomingRelationship.RelationshipId} from {incomingRelationship.SourceId} deleted in DT");
            }
        }
    }
}