using System.Collections.Generic;

namespace HttpClientGenerator
{
    internal class ClientInfo
    {
        public string ClientName { get; set; }

        public IEnumerable<RestEndpointInfo> RestEndpoints { get; set; }
    }
}