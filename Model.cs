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
    public string samplingInterval, queueSize;
}

public class Method : Node
{

}