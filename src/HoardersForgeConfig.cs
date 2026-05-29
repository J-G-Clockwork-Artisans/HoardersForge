using ProtoBuf;

namespace HoardersForge
{
    public class HoardersForgeConfig
    {
        public bool DebugLogging { get; set; } = true;
        public double LossPercentage { get; set; } = 5.0; // 0% to 100%
    }

    [ProtoContract]
    public class ConfigSyncPacket
    {
        [ProtoMember(1)]
        public bool DebugLogging { get; set; }

        [ProtoMember(2)]
        public double LossPercentage { get; set; }
    }

    [ProtoContract]
    public class OpenGuiPacket
    {
    }

    [ProtoContract]
    public class RunTestsPacket
    {
    }
}
