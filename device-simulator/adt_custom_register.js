// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

'use strict';

var crypto = require('crypto');

const dotenv = require('dotenv');
dotenv.config();

var myArgs = process.argv.slice(2);

// Provisioning
var ProvisioningTransport = require('azure-iot-provisioning-device-mqtt').Mqtt;
var SymmetricKeySecurityClient = require('azure-iot-security-symmetric-key').SymmetricKeySecurityClient;
var ProvisioningDeviceClient = require('azure-iot-provisioning-device').ProvisioningDeviceClient;

// IOT Device simulation
var iotHubTransport = require('azure-iot-device-mqtt').Mqtt;
var Client = require('azure-iot-device').Client;
var Message = require('azure-iot-device').Message;

//
// For the public clouds the address of the provisioning host would be: global.azure-devices-provisioning.net
//
var provisioningHost = process.env.PROVISIONING_HOST;

//
// You can find your idScope in the portal overview section for your dps instance.
//
var idScope = process.env.PROVISIONING_IDSCOPE;

//
// The registration id of the device to be registered.
//
var registrationId = process.env.PROVISIONING_REGISTRATION_ID;
if (typeof myArgs[0] == 'string') registrationId = myArgs[0]
console.log('registrationId=' + registrationId);

// The ADT Model Id needs to be provided
var adtModelId = process.env.ADT_MODEL_ID;

var symmetricKey = process.env.PROVISIONING_SYMMETRIC_KEY;

function computeDerivedSymmetricKey(masterKey, regId) {
  return crypto.createHmac('SHA256', Buffer.from(masterKey, 'base64'))
    .update(regId, 'utf8')
    .digest('base64');
}
var symmetricKey = computeDerivedSymmetricKey(symmetricKey, registrationId);

var provisioningSecurityClient = new SymmetricKeySecurityClient(registrationId, symmetricKey);

var provisioningClient = ProvisioningDeviceClient.create(provisioningHost, idScope, new ProvisioningTransport(), provisioningSecurityClient);

// set the custom payload for the DPS call.
var payload = {
  "modelId" : adtModelId
}

// Helper function to generate random number between min and max
function generateRandom(val) {
  return (val + (Math.random() * 2) - 1);
}

// Helper function to print results for an operation
function printErrorFor(op) {
  return function printError(err) {
    if (err) console.log(op + ' error: ' + err.toString());
  };
}

// Sensor data
var temperature = 25;

// Register the device.
provisioningClient.setProvisioningPayload(payload);
provisioningClient.register(function(err, result) {
  if (err) {
    console.log("error registering device: " + err);
  } else {
    console.log('registration succeeded');
    console.log('assigned hub=' + result.assignedHub);
    console.log('deviceId=' + result.deviceId);

    // Open client and send simulated data
    var connectionString = 'HostName=' + result.assignedHub + ';DeviceId=' + result.deviceId + ';SharedAccessKey=' + symmetricKey;
    var hubClient = Client.fromConnectionString(connectionString, iotHubTransport);
    hubClient.open(function(err) {
      if (err) {
        console.error('Could not connect: ' + err.message);
      } else {
        console.log('Client connected');
        // start event data send routing
        var sendInterval = setInterval(function () {
          var message = new Message(JSON.stringify({
            'Temperature': generateRandom(temperature)
          }));
          message.contentEncoding = "utf-8";
          message.contentType = "application/json";

          console.log('Sending device event data:\n' + message.data);
          hubClient.sendEvent(message, printErrorFor('send event'));
        }, 500);
      }
    });
  }
});
