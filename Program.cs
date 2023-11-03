// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.Samples.Common;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Models;
using Azure.ResourceManager.TrafficManager;
using Azure.ResourceManager.TrafficManager.Models;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Azure.ResourceManager.Network;
using System.Text.RegularExpressions;
using System.Security.Cryptography.X509Certificates;

namespace ManageTrafficManager
{
    public class Program
    {
        private static ResourceIdentifier? _resourceGroupId = null;
        private static readonly string certPassword = Utilities.CreatePassword();
        private static readonly List<AzureLocation> regions = new List<AzureLocation>();

        /**
         * Azure traffic manager sample for managing profiles.
         *  - Create a domain
         *  - Create a self-signed certificate for the domain
         *  - Create 5 app service plans in 5 different regions
         *  - Create 5 web apps under the each plan, bound to the domain and the certificate
         *  - Create a traffic manager in front of the web apps
         *  - Disable an endpoint
         *  - Delete an endpoint
         *  - Enable an endpoint
         *  - Change/configure traffic manager routing method
         *  - Disable traffic manager profile
         *  - Enable traffic manager profile
         */
        public static async Task RunSample(ArmClient client)
        {
            var rgName = Utilities.CreateRandomName("rgNEMV_");
            var domainName = Utilities.CreateRandomName("jsdkdemo-") + ".com";
            var appServicePlanNamePrefix = Utilities.CreateRandomName("jplan1_");
            var webAppNamePrefix = Utilities.CreateRandomName("webapp1-") + "-";
            var tmName = Utilities.CreateRandomName("jsdktm-");
            var email = "jondoe@contoso.com";
            var firstName = "Jon";
            var lastName = "Doe";
            var phoneNumber = "+1.12455342242";
            var country = "US";
            var city = "Redmond";
            var address1 = "123 4th Ave";
            var postalCode = "98052";
            var state = "WA";
            var projectPath = Regex.Match(System.IO.Directory.GetCurrentDirectory(), @".*(?=bin\\Debug)").Value;


            // The regions in which web app needs to be created
            //
            regions.Add(AzureLocation.WestUS);
            regions.Add(AzureLocation.EastUS2);
            regions.Add(AzureLocation.EastAsia);
            regions.Add(AzureLocation.JapanEast);
            regions.Add(AzureLocation.NorthCentralUS);
            
            try
            {
                //Get default subscription
                SubscriptionResource subscription = await client.GetDefaultSubscriptionAsync();

                //Create a resource group in the EastUS region
                Utilities.Log("Creating resource group...");
                var rgLro = await subscription.GetResourceGroups().CreateOrUpdateAsync(WaitUntil.Completed, rgName, new ResourceGroupData(AzureLocation.EastUS));
                ResourceGroupResource resourceGroup = rgLro.Value;
                _resourceGroupId = resourceGroup.Id;
                Utilities.Log($"Created a resource group with name: {resourceGroup.Data.Name} ");

                // ============================================================
                // Purchase a domain (will be canceled for a full refund)
                Utilities.Log("Purchasing a domain " + domainName + "...");
                var domainData = new AppServiceDomainData("global")
                {
                    ContactRegistrant = new RegistrationContactInfo(email, firstName, lastName, phoneNumber)
                    {
                        JobTitle = "Registrant",
                        Organization = "Microsoft Inc.",
                        AddressMailing = new RegistrationAddressInfo(address1, city, country, postalCode, state) { }
                    },
                    ContactAdmin = new RegistrationContactInfo(email,firstName,lastName,phoneNumber)
                    {
                        JobTitle = "Admin",
                        Organization = "Microsoft Inc.",
                        AddressMailing = new RegistrationAddressInfo(address1, city, country, postalCode, state) { }
                    },
                    ContactBilling = new RegistrationContactInfo(email,firstName,lastName,phoneNumber)
                    {
                        JobTitle = "Billing",
                        Organization = "Microsoft Inc.",
                        AddressMailing = new RegistrationAddressInfo(address1, city, country, postalCode, state) { }
                    },
                    ContactTech = new RegistrationContactInfo(email,firstName,lastName,phoneNumber)
                    {
                        JobTitle = "Tech",
                        Organization = "Microsoft Inc.",
                        AddressMailing = new RegistrationAddressInfo(address1, city, country, postalCode, state) { }
                    },
                    IsDomainPrivacyEnabled = true,
                    IsAutoRenew = false,
                    Consent = new DomainPurchaseConsent()
                    {
                        AgreedBy = "100.64.152.221",
                        AgreementKeys =
                        {
                            "agreementKey1"
                        },
                        AgreedOn =DateTime.Now
                    }
                };
                var domain = (await resourceGroup.GetAppServiceDomains().CreateOrUpdateAsync(WaitUntil.Completed,domainName,domainData)).Value;
                Utilities.Log("Purchased domain " + domain.Data.Name);
                Utilities.PrintDomain(domain);

                //============================================================
                // Create a self-singed SSL certificate

                var pfxPath = "webapp_" + nameof(ManageTrafficManager).ToLower() + ".pfx";
                Utilities.Log("Creating a self-signed certificate " + pfxPath + "...");
                Utilities.CreateCertificate(domainName, pfxPath, certPassword);
                Utilities.Log("Created self-signed certificate " + pfxPath);

                //============================================================
                // Creates app service in 5 different region

                var appServicePlans = new List<AppServicePlanResource>();
                int id = 0;
                foreach (var region in regions)
                {
                    var planName = appServicePlanNamePrefix + id;
                    Utilities.Log("Creating an app service plan " + planName + " in region " + region + "...");
                    var appServicePlanData = new AppServicePlanData(region)
                    {
                        Sku = new AppServiceSkuDescription()
                        {
                            Name = "B1",
                            Tier = "Basic",
                            Size = "B1"
                        }
                    };                   
                    var appServicePlan =(await resourceGroup.GetAppServicePlans().CreateOrUpdateAsync(WaitUntil.Completed,planName, appServicePlanData)).Value;
                    Utilities.Log("Created app service plan " + planName);
                    Utilities.PrintAppServicePlan(appServicePlan);
                    appServicePlans.Add(appServicePlan);
                    id++;
                }

                //============================================================
                // Creates websites using previously created plan
                var webApps = new List<WebSiteResource>();
                id = 0;
                foreach (var appServicePlan in appServicePlans)
                {
                    var webAppName = webAppNamePrefix + id;
                    var hostName = webAppName + "." + domain.Data.Name;
                    Utilities.Log("Creating a web app " + webAppName + " using the plan " + appServicePlan.Data.Name + "...");
                    var webSiteData = new WebSiteData(appServicePlan.Data.Location)
                    {
                        AppServicePlanId = appServicePlan.Data.Id,
                        SiteConfig = new SiteConfigProperties()
                        {
                            WindowsFxVersion = "PricingTier.StandardS1",
                            NetFrameworkVersion = "v4.6",
                        }
                    };
                    var webApp = (await resourceGroup.GetWebSites().CreateOrUpdateAsync(WaitUntil.Completed, webAppName, webSiteData)).Value;

                    var sslBindingData = new HostNameBindingData()
                    {
                        HostNameType = AppServiceHostNameType.Managed,
                        SiteName = webApp.Data.Name,
                        DomainId = domain.Data.Id,
                        SslState = HostNameBindingSslState.SniEnabled,
                        CustomHostNameDnsRecordType = CustomHostNameDnsRecordType.CName
                    };
                    var sslBinding = (await webApp.GetSiteHostNameBindings().CreateOrUpdateAsync(WaitUntil.Completed, hostName, sslBindingData)).Value;

                    var cert = new X509Certificate(projectPath + "\\" + pfxPath, certPassword).GetRawCertData();
                    Utilities.Log(cert.ToString());
                    var appCertificateData = new AppCertificateData(appServicePlan.Data.Location)
                    {
                        HostNames =
                        {
                            hostName
                        },
                        Password = certPassword,

                        PfxBlob = cert
                    };
                    var appCertificate = (await resourceGroup.GetAppCertificates().CreateOrUpdateAsync(WaitUntil.Completed, hostName, appCertificateData)).Value;

                    var sourceControlInput = new SiteSourceControlData()
                    {
                        RepoUri = new("https://github.com/jianghaolu/azure-site-test"),
                        Branch = "master",
                        IsManualIntegration = true,
                        IsMercurial = false
                    };
                    var sourceControl = (await webApp.GetWebSiteSourceControl().CreateOrUpdateAsync(WaitUntil.Completed,sourceControlInput)).Value;
                    Utilities.Log("Created web app " + webAppName);
                    Utilities.PrintWeb(webApp);
                    webApps.Add(webApp);
                    id++;
                }
                //============================================================
                // Creates a traffic manager profile

                Utilities.Log("Creating a traffic manager profile " + tmName + " for the web apps...");
                var trafficManagerProfileData = new TrafficManagerProfileData()
                {
                    Location = "global",
                    MonitorConfig = new TrafficManagerMonitorConfig()
                    {
                        Protocol = TrafficManagerMonitorProtocol.Http,
                        Port = 80,
                        Path = "/testpath.aspx",
                        IntervalInSeconds = 10,
                        TimeoutInSeconds = 5,
                        ToleratedNumberOfFailures = 2
                    },
                    DnsConfig = new TrafficManagerDnsConfig()
                    {
                        RelativeName = tmName
                    },
                    TrafficRoutingMethod = TrafficRoutingMethod.Priority,
                    ProfileStatus = TrafficManagerProfileStatus.Enabled
                };
                var trafficManagerProfile = (await resourceGroup.GetTrafficManagerProfiles().CreateOrUpdateAsync(WaitUntil.Completed, tmName, trafficManagerProfileData)).Value;
                int priority = 1;
                var endpointType = "AzureEndpoints";
                IList<TrafficManagerEndpointData> tmCreatable =new List<TrafficManagerEndpointData>();
                foreach (var webApp in webApps)
                {
                    var tmDefinition = new TrafficManagerEndpointData()
                    {
                        Name = $"endpoint-{priority}",
                        TargetResourceId = webApp.Data.Id,
                        Priority = priority
                    };
                    tmCreatable.Add(tmDefinition);
                    priority++;
                }
                foreach(var item in tmCreatable)
                {
                    await trafficManagerProfile.GetTrafficManagerEndpoints().CreateOrUpdateAsync(WaitUntil.Completed,endpointType,item.Name,item);
                }
                
                Utilities.Log("Created traffic manager " + trafficManagerProfile.Data.Name);
                Utilities.PrintTrafficManagerProfile(trafficManagerProfile);

                //============================================================
                // Disables one endpoint and removes another endpoint

                Utilities.Log("Disabling and removing endpoint...");
                var endpointDisable = (await trafficManagerProfile.GetTrafficManagerEndpointAsync(endpointType, "endpoint-1")).Value;
                var disableData = new TrafficManagerEndpointData()
                {
                    EndpointStatus = TrafficManagerEndpointStatus.Disabled
                };
                endpointDisable = (await endpointDisable.UpdateAsync(disableData)).Value;
                var endpointRemove = (await trafficManagerProfile.GetTrafficManagerEndpointAsync(endpointType, "endpoint-2")).Value.DeleteAsync(WaitUntil.Completed);
                Utilities.Log("Endpoints updated");

                //============================================================
                // Enables an endpoint

                Utilities.Log("Enabling endpoint...");
                var endpointEnable = (await trafficManagerProfile.GetTrafficManagerEndpointAsync(endpointType, "endpoint-1")).Value;
                var enableData = new TrafficManagerEndpointData()
                {
                    EndpointStatus = TrafficManagerEndpointStatus.Enabled
                };
                endpointEnable = (await endpointEnable.UpdateAsync(enableData)).Value;
                Utilities.Log("Endpoint updated");
                Utilities.Log($"The current endpoint status is: {endpointEnable.Data.EndpointStatus}");

                //============================================================
                // Change/configure traffic manager routing method

                Utilities.Log("Changing traffic manager profile routing method...");
                var updateTrafficData = new TrafficManagerProfileData()
                {
                    TrafficRoutingMethod = TrafficRoutingMethod.Performance
                };
                trafficManagerProfile = (await trafficManagerProfile.UpdateAsync(updateTrafficData)).Value;
                Utilities.Log("Changed traffic manager profile routing method");

                //============================================================
                // Disables the traffic manager profile

                Utilities.Log("Disabling traffic manager profile...");
                var trafficDisableData = new TrafficManagerProfileData()
                {
                    ProfileStatus = TrafficManagerProfileStatus.Disabled
                };
                trafficManagerProfile = (await trafficManagerProfile.UpdateAsync(trafficDisableData)).Value;
                Utilities.Log("Traffic manager profile disabled");

                //============================================================
                // Enables the traffic manager profile

                Utilities.Log("Enabling traffic manager profile...");
                var trafficEnableData = new TrafficManagerProfileData()
                {
                    ProfileStatus = TrafficManagerProfileStatus.Enabled
                };
                trafficManagerProfile = (await trafficManagerProfile.UpdateAsync(trafficEnableData)).Value;
                Utilities.Log("Traffic manager profile enabled");

                //============================================================
                // Deletes the traffic manager profile

                Utilities.Log("Deleting the traffic manger profile...");
                await trafficManagerProfile.DeleteAsync(WaitUntil.Completed);
                Utilities.Log("Traffic manager profile deleted");
            }
            finally
            {
                try
                {
                    if (_resourceGroupId is not null)
                    {
                        Utilities.Log($"Deleting Resource Group...");
                        await client.GetResourceGroupResource(_resourceGroupId).DeleteAsync(WaitUntil.Completed);
                        Utilities.Log($"Deleted Resource Group: {_resourceGroupId.Name}");
                    }
                }
                catch (Exception e)
                {
                    Utilities.Log(e);
                }
            }
        }

        public static async Task Main(string[] args)
        {
            try
            {
                //=================================================================
                // Authenticate

                var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
                var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
                var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
                var subscription = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
                //ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                var credential = new InteractiveBrowserCredential();//not authorization create domain
                ArmClient client = new ArmClient(credential, subscription);

                await RunSample(client);
            }
            catch (Exception e)
            {
                Utilities.Log(e);
            }
        }
    }
}
