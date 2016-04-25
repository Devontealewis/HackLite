using System;

//Setting was helped put together with Thomas Jones
namespace HackLite
{
  
        public class Settings
        {
            public string domainName;
            public string gateway;
            public string subnet;
            public string[] dns;
            public string NICName;

            public Settings() : this("", "0.0.0.0", "255.255.255.0", "", "8.8.8.8", "8.8.4.4") { }
            public Settings(string domainName, string gateway, string subnet, string NICName,params string[] dns)
            {
                gateway = gateway.Trim();
                subnet = subnet.Trim();

                if (!ScanTable.validIp(gateway))
                    throw new Exception("Invalid Gateway Address");
                if (!ScanTable.validIp(subnet))
                    throw new Exception("Invalid Subnet Address");
               
                this.domainName = domainName;
                this.gateway = gateway;
                this.subnet = subnet;
                this.NICName = NICName;
            }

          

           
           
           
        

   

       
           
            
      
        }
    }

