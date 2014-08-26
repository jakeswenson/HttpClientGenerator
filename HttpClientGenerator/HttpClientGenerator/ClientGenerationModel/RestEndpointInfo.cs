using System.Collections.Generic;
using RestSharp;

namespace HttpClientGenerator.ClientGenerationModel
{
    internal class RestEndpointInfo
    {
        public string Name { get; set; }

        public string Uri { get; set; }

        public Method Method { get; set; }

        public IEnumerable<EndpointParameter> Parameters { get; set; }

        public string ReturnType { get; set; }
    }
}