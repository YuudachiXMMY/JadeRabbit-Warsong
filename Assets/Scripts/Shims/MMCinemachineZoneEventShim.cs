namespace MoreMountains.Tools
{
    public enum MMCinemachineZoneEventTypes { Enter, Exit }

    public struct MMCinemachineZoneEvent
    {
        public MMCinemachineZoneEventTypes EventType;
        public MMCinemachineZoneEventTypes Type;
        public MMCinemachineZoneEvent(MMCinemachineZoneEventTypes eventType)
        {
            EventType = eventType;
            Type = eventType;
        }
    }
}