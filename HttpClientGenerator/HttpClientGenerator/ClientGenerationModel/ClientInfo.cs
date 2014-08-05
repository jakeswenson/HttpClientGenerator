using System.Collections.Generic;

namespace HttpClientGenerator.ClientGenerationModel
{
    internal class ClientInfo
    {
        public string ClientName { get; set; }

        public IEnumerable<RestEndpointInfo> RestEndpoints { get; set; }
    }
}