using System;
using System.Collections.Generic;
using Rackspace.Cloud.Server.Agent.Configuration;
using Rackspace.Cloud.Server.Agent.Interfaces;
using Rackspace.Cloud.Server.Agent.Utilities;
using Rackspace.Cloud.Server.Agent.WMI;
using Rackspace.Cloud.Server.Common.Logging;

namespace Rackspace.Cloud.Server.Agent.Actions
{
    public interface ISetNetworkInterface {
        void Execute(NetworkInterface networkInterface);
    }

    public class SetNetworkInterface : ISetNetworkInterface {
        private readonly IExecutableProcessQueue _executableProcessQueue;
        private readonly IWmiMacNetworkNameGetter _wmiMacNetworkNameGetter;
        private readonly ILogger _logger;

        public SetNetworkInterface(IExecutableProcessQueue executableProcessQueue, IWmiMacNetworkNameGetter wmiMacNetworkNameGetter, ILogger logger) {
            _executableProcessQueue = executableProcessQueue;
            _wmiMacNetworkNameGetter = wmiMacNetworkNameGetter;
            _logger = logger;
        }

        public void Execute(NetworkInterface networkInterface) {

            var nameAndMacs = _wmiMacNetworkNameGetter.Get();
            if (WereInterfacesEnabled(nameAndMacs)) nameAndMacs = _wmiMacNetworkNameGetter.Get();

            LogLocalInterfaces(nameAndMacs);

            string macAddress = networkInterface.mac.ToUpper();
            if (!nameAndMacs.Values.Contains(macAddress)) throw new ApplicationException(String.Format("Interface with MAC Addres {0} not found on machine", macAddress));
            
            string interfaceName = nameAndMacs.FindKey(macAddress);

            CleanseInterfaceForSetup(interfaceName);
            SetupInterface(interfaceName, networkInterface);

            if (networkInterface.dns != null && networkInterface.dns.Length > 0) {
                CleanseDnsForSetup(interfaceName);
                SetupDns(interfaceName, networkInterface);
            }
                    
            if(interfaceName != networkInterface.label)
                _executableProcessQueue.Enqueue("netsh", String.Format("interface set interface name=\"{0}\" newname=\"{1}\"", interfaceName, networkInterface.label));
            
            _executableProcessQueue.Go();
        }

        private void LogLocalInterfaces(IDictionary<string, string> nameAndMacs) {
            _logger.Log("Network Interfaces found locally:");
            foreach (var networkInterface in nameAndMacs) {
                _logger.Log(String.Format("{0} ({1})", networkInterface.Key, networkInterface.Value));
            } 
        }

        private void SetupInterface(string interfaceName, NetworkInterface networkInterface) {
            var primaryIpHasBeenAssigned = false;
            for (var i = 0; i != networkInterface.ips.Length; i++) {
                if (networkInterface.ips[i].enabled != "1") continue;
                if (!string.IsNullOrEmpty(networkInterface.gateway) && !primaryIpHasBeenAssigned) {
                    _executableProcessQueue.Enqueue("netsh",
                                                    String.Format(
                                                        "interface ip add address name=\"{0}\" addr={1} mask={2} gateway={3} gwmetric=2",
                                                        interfaceName, networkInterface.ips[i].ip, networkInterface.ips[i].netmask, networkInterface.gateway));
                    primaryIpHasBeenAssigned = true; 
                    continue;
                }

                _executableProcessQueue.Enqueue("netsh", String.Format("interface ip add address name=\"{0}\" addr={1} mask={2}",
                                                                       interfaceName, networkInterface.ips[i].ip, networkInterface.ips[i].netmask));
            }
        }

        private void SetupDns(string interfaceName, NetworkInterface networkInterface) {
            for (var i = 0; i != networkInterface.dns.Length; i++) {
                _executableProcessQueue.Enqueue("netsh", String.Format("interface ip add dns name=\"{0}\" addr={1} index={2}",
                                                                       interfaceName, networkInterface.dns[i], i + 1));
            }
        }

        private void CleanseInterfaceForSetup(string interfaceName) {
            _executableProcessQueue.Enqueue("netsh", string.Format("interface ip set address name=\"{0}\" source=dhcp", interfaceName), new[] { "0", "1" });
        }

        private void CleanseDnsForSetup(string interfaceName) {
            _executableProcessQueue.Enqueue("netsh", string.Format("interface ip set dns name=\"{0}\" source=dhcp", interfaceName), new[] { "0", "1" });
        }

        private bool WereInterfacesEnabled(IEnumerable<KeyValuePair<string, string>> nameAndMacs) {
            var wereMacsEnabled = false;
            foreach (var nameAndMac in nameAndMacs) {
                if (nameAndMac.Value != string.Empty) continue;
                _executableProcessQueue.Enqueue("netsh", String.Format("interface set interface name=\"{0}\" admin=ENABLED", nameAndMac.Key));
                _executableProcessQueue.Go();
                wereMacsEnabled = true;
            }

            return wereMacsEnabled;
        }
    }
}