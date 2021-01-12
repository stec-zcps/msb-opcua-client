public class Service
{
    [Newtonsoft.Json.JsonProperty("@class")]
    public string class_;
    public string uuid;
    public string name;
    public string description;
    public string token;
    public string target_interface;
}

public class Node
{
    public string nodeId, name;
    public bool handleAsComplexObject;
}

public class ReadNode : Node
{

}

public class WriteNode : Node
{

}

public class MonitorNode : Node
{
    public int samplingInterval = 1000;
    public uint queueSize = 1;
}

public class Method : Node
{

}