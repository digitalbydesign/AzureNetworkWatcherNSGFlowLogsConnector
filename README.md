# Azure Function ARM Template for NSG Flow Log Ingestion 

The NwNSGGlowLogs branch contains a working version of the deployment template, tailored for a real version of a function that transmits Azure Network Watcher NSG Flow Logs to EventHub.

NOTE: Native support for event hubs is not yet available, but would be the preferred method.

[![Deploy to Azure](http://azuredeploy.net/deploybutton.png)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fdigitalbydesign%2FAzureNetworkWatcherNSGFlowLogsConnector%2Ffeature%2Fcampus%2FazureDeploy.json)

## Overview

The steps to fully implement the Azure Network Watcher NSG Flow Logs Connector are:
* Gather the settings below.
* Click the "Deploy to Azure" button above.
* Authenticate to the Azure Portal (if necessary)
* Fill in the form with the setting values
* Wait a few minutes for the function to be created and deployed

## Settings

* AppName                     - this is the name of the function app. In the Azure Portal, this is the name that will appear in the list of resources.
   Example: ```MyNSGApp```
* appServicePlan              - "ServicePlan" or "Consumption".
   If you select "ServicePlan", an App Service Plan will be created and you will be billed accordingly. If you select "Consumption", you will be billed based on the Consumption plan.
* appServicePlanTier          - "Free", "Shared", "Basic", "Standard", "Premium", "PremiumV2"
   Example: ```Standard```
   (only relevant for ServicePlan)
* appServicePlanName          - depends on tier, for full details see "Choose your pricing tier" in the portal on an App service plan "Scale up" applet.
   Example: For standard tier, "S1", "S2", "S3" are options for plan name
   (only relevant for ServicePlan)
* appServicePlanCapacity      - how many instances do you want to set for the upper limit?
   Example: For standard tier, S2, set a value from 1 to 10
   (only relevant for ServicePlan)
* githubRepoURL                     - this is the URL of the repo that contains the function app source. You would put your fork's address here.
   Example: ```https://github.com/microsoft/AzureNetworkWatcherNSGFlowLogsConnector```
* githubRepoBranch                  - this is the name of the branch containing the code you want to deploy.
   Example: ```master```
* nsgSourceDataConnection     - a storage account connection string
   Example: ```DefaultEndpointsProtocol=https;AccountName=yyy;AccountKey=xxx;EndpointSuffix=core.windows.net```
* outputBinding               - Points to the destination service - the service that will receive the NSG flow log data. Options is "eventhub".
   Example: ```eventhub```
* cefLogAccount               - a storage account connection string - account into which trace logs of incoming json and outgoing cef are dropped
   Example: ```DefaultEndpointsProtocol=https;AccountName=yyy;AccountKey=xxx;EndpointSuffix=core.windows.net```
* eventHubConnection          - connection string for your event hub namespace
   Example: ```Endpoint=sb://my.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=key```
* eventHubName                - name of your event hub within the hub namespace
   Example: ```insights-logs-nsgflowlogs```
