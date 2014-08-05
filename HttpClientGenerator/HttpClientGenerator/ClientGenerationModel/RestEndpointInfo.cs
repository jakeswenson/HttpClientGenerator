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

    internal class EndpointParameter
    {
        public string Name { get; set; }

        public string TypeName { get; set; }
    }

    class SimpleType
    {
        public string Name { get; set; }

        public IEnumerable<SimpleTypeMember> Members { get; set; }
    }

    internal class SimpleTypeMember
    {
        public string TypeName { get; set; }

        public string Name { get; set; }
    }
}