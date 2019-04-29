using System;

namespace Ztm.Configuration
{
    public class ZcoinConfiguration
    {
        public ZcoinRpcConfiguration Rpc { get; set; }
    }

    public class ZcoinRpcConfiguration
    {
        public Uri Address { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}