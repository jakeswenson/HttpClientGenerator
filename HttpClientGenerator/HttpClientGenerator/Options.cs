using PowerArgs;

namespace HttpClientGenerator
{
    public class Options
    {
        [ArgRequired]
        [ArgShortcut("p")]
        [ArgPosition(0)]
        [ArgDescription("Project (csproj) path containing ApiControllers")]
        public string ProjectPath { get; set; }
    }
}