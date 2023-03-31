namespace SimpleTcp.Core
{
    static class Constants
    {
        public const float PING_INTERVAL = 10.0f;
    }
    
    public enum DisconnectionType
    {
        TimeOut,
        Forced,
        Correctly
    }
    
    public enum EncodingFormat
    {
        Unicode,
        BigEndianUnicode,
        UTF7,
        UTF8,
        UTF32,
        ASCII
    }
    
    
}