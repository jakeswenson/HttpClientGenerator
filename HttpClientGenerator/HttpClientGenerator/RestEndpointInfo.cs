using System.Collections.Generic;
using RestSharp;

namespace HttpClientGenerator
{
    internal class RestEndpointInfo
    {
        public string Name { get; set; }

        public string Uri { get; set; }

        public Method Method { get; set; }

        public IEnumerable<object> Arguments { get; set; }
    }
}