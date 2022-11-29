// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Azure;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Azure.Devices.Provisioning.Service;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Samples.AdtIothub
{
    public static class DpsAdtAllocationFunc
    {
        private const string adtAppId = "https://digitaltwins.azure.net";
        private static string adtInstanceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");
        private static readonly HttpClient singletonHttpClientInstance = new HttpClient();

        [FunctionName("DpsAdtAllocationFunc")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            // Get request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            log.LogDebug($"Request.Body: {requestBody}");
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            // Get registration ID of the device
            string regId = data?.deviceRuntimeContext?.registrationId;

            bool fail = false;
            string message = "Uncaught error";
            var response = new ResponseObj();

            // Must have unique registration ID on DPS request
            if (regId == null)
            {
                message = "Registration ID not provided for the device.";
                log.LogInformation("Registration ID: NULL");
                fail = true;
            }
            else
            {
                string[] hubs = data?.linkedHubs.ToObject<string[]>();

                // Must have hubs selected on the enrollment
                if (hubs == null
                    || hubs.Length < 1)
                {
                    message = "No hub group defined for the enrollment.";
                    log.LogInformation("linkedHubs: NULL");
                    fail = true;
                }
                else
                {
                    // Find or create twin based on the provided registration ID and model ID
                    dynamic payloadContext = data?.deviceRuntimeContext?.payload;
                    string dtmi = payloadContext.modelId;
                    log.LogDebug($"payload.modelId: {dtmi}");
                    string dtId = await FindOrCreateTwinAsync(dtmi, regId, log);

                    // Get first linked hub (TODO: select one of the linked hubs based on policy)
                    response.iotHubHostName = hubs[0];

                    // Specify the initial tags for the device.
                    var tags = new TwinCollection();
                    tags["dtmi"] = dtmi;
                    tags["dtId"] = dtId;

                    // Specify the initial desired properties for the device.
                    var properties = new TwinCollection();

                    // Add the initial twin state to the response.
                    var twinState = new TwinState(tags, properties);
                    response.initialTwin = twinState;
                }
            }

            log.LogDebug("Response: " + ((response.iotHubHostName != null)? JsonConvert.SerializeObject(response) : message));

            return fail
                ? new BadRequestObjectResult(message)
                : (ActionResult)new OkObjectResult(response);
        }

        public static async Task<string> FindOrCreateTwinAsync(string dtmi, string regId, ILogger log)
        {
            // Create Digital Twins client
            var cred = new DefaultAzureCredential();
            var client = new DigitalTwinsClient(new Uri(adtInstanceUrl), cred);

            // Find existing DigitalTwin with registration ID
            try
            {
                // Get DigitalTwin with Id 'regId'
                BasicDigitalTwin existingDt = await client.GetDigitalTwinAsync<BasicDigitalTwin>(regId).ConfigureAwait(false);

                // Check to make sure it is of the correct model type
                if (StringComparer.OrdinalIgnoreCase.Equals(dtmi, existingDt.Metadata.ModelId))
                {
                    log.LogInformation($"DigitalTwin {existingDt.Id} already exists");
                    return existingDt.Id;
                }

                // Found DigitalTwin but it is not of the correct model type
                log.LogInformation($"Found DigitalTwin {existingDt.Id} but it is not of model {dtmi}");
            }
            catch(RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                log.LogDebug($"Did not find DigitalTwin {regId}");
            }

            // Either the DigitalTwin was not found, or we found it but it is of a different model type
            // Create or replace it with what it needs to be, meaning if it was not found a brand new DigitalTwin will be created
            // and if it was of a different model, it will replace that existing DigitalTwin
            // If it was intended to only create the DigitalTwin if there is no matching DigitalTwin with the same Id,
            // ETag.All could have been used as the ifNonMatch parameter to the CreateOrReplaceDigitalTwinAsync method call.
            // Read more in the CreateOrReplaceDigitalTwinAsync documentation here:
            // https://docs.microsoft.com/en-us/dotnet/api/azure.digitaltwins.core.digitaltwinsclient.createorreplacedigitaltwinasync?view=azure-dotnet
            BasicDigitalTwin dt = await client.CreateOrReplaceDigitalTwinAsync(
                regId, 
                new BasicDigitalTwin
                {
                    Metadata = { ModelId = dtmi },
                    Contents = 
                    {
                        { "Temperature", 0.0 }
                    }
                }
            ).ConfigureAwait(false);

            log.LogInformation($"Digital Twin {dt.Id} created.");
            return dt.Id;
        }
    }

    /// <summary>
    /// Expected function result format
    /// </summary>
    public class ResponseObj
    {
        public string iotHubHostName { get; set; }
        public TwinState initialTwin { get; set; }
    }
}
