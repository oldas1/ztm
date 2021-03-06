using System.IO;
using System.Text;
using NBitcoin.RPC;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Ztm.Zcoin.Testing
{
    public static class RPCExceptionTesting
    {
        public static RPCException BuildException(RPCErrorCode code, string message, object response)
        {
            var jsonSerializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new SnakeCaseNamingStrategy()
                }
            };

            var rawMessage = JsonConvert.SerializeObject(response, jsonSerializerSettings);
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(rawMessage)))
            {
                return new RPCException(code, message, RPCResponse.Load(stream));
            };
        }
    }
}