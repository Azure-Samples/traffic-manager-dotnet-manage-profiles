---
page_type: sample
languages:
- csharp
products:
- azure
extensions:
  services: Traffic-Manager
  platforms: dotnet
---

# Getting started on managing Traffic Manager Profiles in C# #

 Azure traffic manager sample for managing profiles.
  - Create a domain
  - Create a self-signed certificate for the domain
  - Create 5 app service plans in 5 different regions
  - Create 5 web apps under the each plan, bound to the domain and the certificate
  - Create a traffic manager in front of the web apps
  - Disable an endpoint
  - Delete an endpoint
  - Enable an endpoint
  - Change/configure traffic manager routing method
  - Disable traffic manager profile
  - Enable traffic manager profile


## Running this Sample ##

To run this sample:

Set the environment variable `CLIENT_ID`,`CLIENT_SECRET`,`TENANT_ID`,`SUBSCRIPTION_ID` with the full path for an auth file. See [how to create an auth file](https://github.com/Azure/azure-libraries-for-net/blob/master/AUTH.md).
    git clone https://github.com/Azure-Samples/traffic-manager-dotnet-manage-profiles.git

    cd traffic-manager-dotnet-manage-profiles

    dotnet build

    bin\Debug\net452\ManageTrafficManager.exe

## More information ##

[Azure Management Libraries for C#](https://github.com/Azure/azure-sdk-for-net/)
[Azure .Net Developer Center](https://azure.microsoft.com/en-us/develop/net/)
If you don't have a Microsoft Azure subscription you can get a FREE trial account [here](http://go.microsoft.com/fwlink/?LinkId=330212)

---

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.