# Azure Digital Twin and IoT Hub Integration Sample

This is a sample project to show possible patterns for the automatic integration of the new Azure Digital Twins and IoT Hub. The following scenarios are implemented:

* An Azure Device Provisioning Service custom allocation Azure Function to look up details for a device in Azure Digital Twins and inform the provisioning process.
* A simulated device device that sends the necessary information to DPS for the allocation decision.
* An Azure Function that implements the automatic deletion of an Azure Digital Twin entity when the linked device is deleted in IoT Hub.
* An Azure Function that maps...

## How to use this sample

* Set up an Azure Digital Twin Service using the [documentation](https://docs.microsoft.com/en-us/azure/digital-twins/how-to-set-up-instance-scripted)
* Create an IoT Hub, Device Provisioning Service and an Azure Functions service.
* Download and install Visual Studio Code and the following extensions:
  * Azure IoT Tools
  * Azuer Functions
* Clone this repo
* `cd functions`
* `dotnet restore`
* Deploy the application to your Azure Functions
* Create a Group Enrollment in DPS and link to the `DpsAdtAllocationFunc` in your functions instance
* Customise the device simulator code with the credentials for your group enrollment and run the code
