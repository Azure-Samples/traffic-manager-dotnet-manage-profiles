// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Azure.Core;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Azure.ResourceManager.Samples.Common
{
    public static class Utilities
    {
        public static Action<string> LoggerMethod { get; set; }
        public static Func<string> PauseMethod { get; set; }
        public static string ProjectPath { get; set; }
        private static Random _random => new Random();
        public static bool IsRunningMocked { get; set; }


        static Utilities()
        {
            LoggerMethod = Console.WriteLine;
            PauseMethod = Console.ReadLine;
            ProjectPath = ".";
        }

        public static void Log(string message)
        {
            LoggerMethod.Invoke(message);
        }

        public static void Log(object obj)
        {
            if (obj != null)
            {
                LoggerMethod.Invoke(obj.ToString());
            }
            else
            {
                LoggerMethod.Invoke("(null)");
            }
        }

        public static void Log()
        {
            Utilities.Log("");
        }

        public static string ReadLine() => PauseMethod.Invoke();

        public static string CreateRandomName(string namePrefix) => $"{namePrefix}{_random.Next(9999)}";

        public static string CreatePassword() => "azure12345QWE!";

        public static string CreateUsername() => "tirekicker";

        public static void CreateCertificate(string domainName, string pfxPath, string password)
        {
            if (!IsRunningMocked)
            {
                string args = string.Format(
                    @".\createCert.ps1 -pfxFileName {0} -pfxPassword ""{1}"" -domainName ""{2}""",
                    pfxPath,
                    password,
                    domainName);
                ProcessStartInfo info = new ProcessStartInfo("powershell", args);
                string assetPath = Path.Combine(ProjectPath, "Asset");
                info.WorkingDirectory = assetPath;
                Process process = Process.Start(info);
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    // call "Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy Bypass" in powershell if you fail here

                    Utilities.Log("powershell createCert.ps1 script failed");
                }
            }
            else
            {
                //File.Copy(
                //    Path.Combine(Utilities.ProjectPath, "Asset", "SampleTestCertificate.pfx"),
                //    Path.Combine(Utilities.ProjectPath, "Asset", pfxPath),
                //    overwrite: true);
            }
        }

        public static async Task<List<T>> ToEnumerableAsync<T>(this IAsyncEnumerable<T> asyncEnumerable)
        {
            List<T> list = new List<T>();
            await foreach (T item in asyncEnumerable)
            {
                list.Add(item);
            }
            return list;
        }

        public static async Task<NetworkInterfaceResource> CreateVirtualNetworkInterface(ResourceGroupResource resourceGroup, ResourceIdentifier subnetId, string publicIPName = null)
        {
            publicIPName = publicIPName is null ? CreateRandomName("azcrpip") : publicIPName;
            string nicName = CreateRandomName("nic");

            // Create a public ip
            var publicIPInput = new PublicIPAddressData()
            {
                Location = resourceGroup.Data.Location,
                PublicIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
            };
            var publicIPLro = await resourceGroup.GetPublicIPAddresses().CreateOrUpdateAsync(WaitUntil.Completed, publicIPName, publicIPInput);
            var publicIPId = publicIPLro.Value.Data.Id;

            // Create a network interface
            var subnetInput = new NetworkInterfaceData()
            {
                Location = resourceGroup.Data.Location,
                IPConfigurations =
                {
                    new NetworkInterfaceIPConfigurationData()
                    {
                        Name = "default-config",
                        PrivateIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                        PublicIPAddress = new PublicIPAddressData()
                        {
                            Id = publicIPId
                        },
                        Subnet = new SubnetData()
                        {
                            Id = subnetId
                        }
                    }
                }
            };
            var networkInterfaceLro = await resourceGroup.GetNetworkInterfaces().CreateOrUpdateAsync(WaitUntil.Completed, nicName, subnetInput);
            return networkInterfaceLro.Value;
        }

        public static async Task<VirtualMachineResource> CreateVirtualMachine(ResourceGroupResource resourceGroup, ResourceIdentifier nicId, string vmName = null)
        {
            vmName = vmName is null ? Utilities.CreateRandomName("vm") : vmName;
            VirtualMachineCollection vmCollection = resourceGroup.GetVirtualMachines();
            VirtualMachineData vmInput = new VirtualMachineData(resourceGroup.Data.Location)
            {
                HardwareProfile = new VirtualMachineHardwareProfile()
                {
                    VmSize = VirtualMachineSizeType.StandardDS1V2
                },
                StorageProfile = new VirtualMachineStorageProfile()
                {
                    ImageReference = new ImageReference()
                    {
                        Publisher = "MicrosoftWindowsDesktop",
                        Offer = "Windows-10",
                        Sku = "win10-21h2-ent",
                        Version = "latest",
                    },
                    OSDisk = new VirtualMachineOSDisk(DiskCreateOptionType.FromImage)
                    {
                        OSType = SupportedOperatingSystemType.Windows,
                        Name = CreateRandomName("myVMOSdisk"),
                        Caching = CachingType.ReadOnly,
                        ManagedDisk = new VirtualMachineManagedDisk()
                        {
                            StorageAccountType = StorageAccountType.StandardLrs,
                        },
                    },
                },
                OSProfile = new VirtualMachineOSProfile()
                {
                    AdminUsername = CreateUsername(),
                    AdminPassword = CreatePassword(),
                    ComputerName = vmName,
                },
                NetworkProfile = new VirtualMachineNetworkProfile()
                {
                    NetworkInterfaces =
                    {
                        new VirtualMachineNetworkInterfaceReference()
                        {
                            Id = nicId,
                            Primary = true,
                        }
                    }
                },
            };
            var vmLro = await vmCollection.CreateOrUpdateAsync(WaitUntil.Completed, vmName, vmInput);
            return vmLro.Value;
        }
    }
}