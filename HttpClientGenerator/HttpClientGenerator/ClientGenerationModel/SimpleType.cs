using System.Collections.Generic;

namespace HttpClientGenerator.ClientGenerationModel
{
    class SimpleType
    {
        public string Name { get; set; }

        public IEnumerable<SimpleTypeMember> Members { get; set; }
    }
}