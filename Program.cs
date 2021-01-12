using System;
using System.Collections.Generic;
using Fraunhofer.IPA.MSB.Client.API.Model;

public class Program
{
    public static void msbCallback_Connected(object sender, System.EventArgs e)
    {
        msbClient.RegisterAsync(msbApplication);
    }  

    public static void msbCallback_ConfigurationEvent(object sender, Fraunhofer.IPA.MSB.Client.API.EventArgs.ConfigurationParameterReceivedEventArgs e)
    {
        reconfigure = true;
    }        

    public static void msbCallback_FunctionReadNode(FunctionCallInfo info)
    {            
        var nodeId = info.Function.Id.Substring(("read_").Length);
        object val;
        EventData eventData;

        if(opcUaClient.readToObject(nodeId, out val))
        {
            eventData = new EventData(readEvents[nodeId]) {
                CorrelationId = info.CorrelationId,
                Value = val
            };
        } else {
            eventData = new EventData(msbEvent_OpcUaError) {
                CorrelationId = info.CorrelationId
            };
        }
                    
        msbClient.PublishAsync(msbApplication, eventData);            
    }

    public static void msbCallback_FunctionCallMethod(List<object> parameter, FunctionCallInfo info)
    {
        var nodeId = info.Function.Id.Substring(("method_").Length);

        opcUaClient.callMethod(nodeId);
    }
    public static void msbCallback_FunctionWriteNode_Generic<T>([Fraunhofer.IPA.MSB.Client.API.Attributes.MsbFunctionParameter(Name = "val")]T val, FunctionCallInfo info)
    {
        var nodeId = info.Function.Id.Substring(("write_").Length);
        
        opcUaClient.writeNode(nodeId, val);
    }

    public static System.Reflection.MethodInfo fitCbFunctionWithDatatype(string nodeId)
    {
        Opc.Ua.Node node;
        
        if (!opcUaClient.readToNode(nodeId, out node)) return null;

        if (node.GetType() != typeof(Opc.Ua.VariableNode)) return null;

        var dt = OpcUaClient.TranslateNodeIdtoDatatype(((Opc.Ua.VariableNode)node).DataType);

        if (dt == null) return null;

        return typeof(Program).GetMethod("msbCallback_WriteNode_Generic").MakeGenericMethod(dt);
    }

    public static Event createEventforNodeId(string nodeId)
    {
        Opc.Ua.Node node;
        
        if (!opcUaClient.readToNode(nodeId, out node)) return null;

        if (node.GetType() != typeof(Opc.Ua.VariableNode)) return null;

        var dt = OpcUaClient.TranslateNodeIdtoDatatype(((Opc.Ua.VariableNode)node).DataType);

        if (dt == null) return null;

        return new Event(nodeId, node.DisplayName + "(" + node.BrowseName + ")", "", dt);
    }

    private static void opcuaCallback_monitoredItemNotification(Opc.Ua.Client.MonitoredItem item, Opc.Ua.Client.MonitoredItemNotificationEventArgs e)
    {
        string id = item.ResolvedNodeId.ToString();

        var me = monitoredEvents[id];

        foreach (var value in item.DequeueValues())
        {
            var ev = new EventData(me);
            ev.Value = value.Value;
            try
            {
                msbClient.PublishAsync(msbApplication, ev);
            } catch {

            }                
        }
    }

    public static Fraunhofer.IPA.MSB.Client.Websocket.MsbClient msbClient;

    public static Application msbApplication;

    public static OpcUaClient opcUaClient;

    public static Dictionary<string, Event> monitoredEvents, readEvents;

    public static Dictionary<String, ConfigurationParameterValue> ConfigParametersFromObject(string path, char splitting, object obj)
    {
        var r = new Dictionary<String, ConfigurationParameterValue>();

        if (obj == null) return r;

        var type = obj.GetType();

        if (type == typeof(int) || type == typeof(uint) || type == typeof(string) || type == typeof(bool) || type == typeof(float) || type == typeof(double))
        {
            r.Add(path, new ConfigurationParameterValue(obj));
        } else if(type.BaseType == typeof(System.Object) ) {
            if (type.IsGenericType)
            {                    
                var param = new ConfigurationParameterValue(obj);
                r.Add(path, param);                    
            } else {
                var fields = type.GetFields();
                foreach(var field in fields)
                {
                    var r_sub = ConfigParametersFromObject(path + splitting + field.Name, splitting, field.GetValue(obj));
                    foreach (var r_sub_ in r_sub) r.Add(r_sub_.Key, r_sub_.Value);
                }
            }
        } else {

        }

        return r;
    }

    public static void opcuaCallback_Connected(object sender, System.EventArgs e)
    {
        if (!msbClient.IsConnected()) return;

        var resp = new EventData(msbEvent_OpcUaConnected);
        msbClient.PublishAsync(msbApplication, resp);
    }

    public static void opcuaCallback_Disconnected(object sender, System.EventArgs e)
    {
        if (!msbClient.IsConnected()) return;

        var resp = new EventData(msbEvent_OpcUaDisconnected);
        msbClient.PublishAsync(msbApplication, resp);
    }

    static Event msbEvent_OpcUaConnected, msbEvent_OpcUaDisconnected, msbEvent_OpcUaError;

    static void addStandardConfigurationParameters()
    {
        msbApplication.AddConfigurationParameter("opcua.server.url", new ConfigurationParameterValue(""));
        msbApplication.AddConfigurationParameter("opcua.server.security", new ConfigurationParameterValue(""));
        msbApplication.AddConfigurationParameter("opcua.server.user", new ConfigurationParameterValue(""));
        msbApplication.AddConfigurationParameter("opcua.server.password", new ConfigurationParameterValue(""));
        msbApplication.AddConfigurationParameter("opcua.subscription.publishingInterval", new ConfigurationParameterValue((int)1000));
        msbApplication.AddConfigurationParameter("opcua.client.readNodes", new ConfigurationParameterValue(new List<ReadNode>()));
        msbApplication.AddConfigurationParameter("opcua.client.writeNodes", new ConfigurationParameterValue(new List<WriteNode>()));
        msbApplication.AddConfigurationParameter("opcua.client.monitorNodes", new ConfigurationParameterValue(new List<MonitorNode>()));
        msbApplication.AddConfigurationParameter("opcua.client.methods", new ConfigurationParameterValue(new List<Method>()));
    }

    static void addStandardEvents()
    {
        msbEvent_OpcUaDisconnected = new Event("OPCUA_No_Conn", "No connection to OPC UA Server", "", typeof(string));
        msbEvent_OpcUaConnected = new Event("OPCUA_Conn", "Connection to OPC UA Server", "", typeof(string));
        msbEvent_OpcUaError = new Event("OPCUA_Error", "OPC UA Error", "", typeof(string));
        msbApplication.AddEvent(msbEvent_OpcUaDisconnected);
        msbApplication.AddEvent(msbEvent_OpcUaConnected);
        msbApplication.AddEvent(msbEvent_OpcUaError);
    }

    public static bool run, reconfigure;
    static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine("Path to service description file required as first argument.");
            return;
        }

        if (!System.IO.File.Exists(args[0]))
        {
            Console.WriteLine("Service description file does not exist. Creating template with new UUID. Exiting afterwards.");
            var s = new Service()
            {
                class_ = "Application",
                uuid = System.Guid.NewGuid().ToString(),
                name = "OPCUA-Websocket-Client",
                description = "OPCUA-Websocket-Client",
                token = System.Guid.NewGuid().ToString(),
                target_interface = "http://ws.msb.dia.cell.vfk.fraunhofer.de"
            };
            
            System.IO.File.WriteAllText(args[0].ToString(), Newtonsoft.Json.JsonConvert.SerializeObject(s));

            return;
        }

        Start:

        var c_file = System.IO.File.ReadAllText(args[0]);
        var c = Newtonsoft.Json.JsonConvert.DeserializeObject<Service>(c_file);

        msbApplication = new Application(c.uuid, c.name, c.token, c.token);

        if (!System.IO.File.Exists(msbApplication.ConfigurationPersistencePath))
        {
            addStandardConfigurationParameters();
            msbApplication.Configuration.SaveToFile(msbApplication.ConfigurationPersistencePath);
        } else {

        }

        msbApplication.AutoPersistConfiguration = true;

        addStandardEvents();

        opcUaClient = new OpcUaClient((string)msbApplication.Configuration.Parameters["opcua.server.url"].Value, c.name);
        opcUaClient.CreateSession();
    
        var monitorNodes = ((Newtonsoft.Json.Linq.JArray)msbApplication.Configuration.Parameters["opcua.client.monitorNodes"].Value).ToObject<List<MonitorNode>>();

        if (monitorNodes != null)
        {
            monitoredEvents = new Dictionary<string, Event>();
            
            foreach (var d in monitorNodes)
            {
                if (d == null) continue;

                var e = createEventforNodeId(d.nodeId);

                if (e == null) continue;

                e.Id = "monitored_" + e.Id;
                monitoredEvents.Add(d.nodeId, e);
                msbApplication.AddEvent(e);

                opcUaClient.monitorItem(d.nodeId, d.samplingInterval, d.queueSize, opcuaCallback_monitoredItemNotification);               
            }

            opcUaClient.generateSubscription((int)msbApplication.Configuration.Parameters["opcua.subscription.publishingInterval"].Value);
        }

        var readNodes = ((Newtonsoft.Json.Linq.JArray)msbApplication.Configuration.Parameters["opcua.client.readNodes"].Value).ToObject<List<ReadNode>>();

        if (readNodes != null)
        {
            var m = typeof(Program).GetMethod("msbCallback_FunctionReadNode");
            
            readEvents = new Dictionary<string, Event>();

            foreach (var d in readNodes)
            {
                if (d == null) continue;

                var e = createEventforNodeId(d.nodeId);

                if (e == null) continue;

                e.Id = "read_" + e.Id;
                e.Name = "Response - " + e.Name;
                readEvents.Add(d.nodeId, e);
                msbApplication.AddEvent(e);
                var resp = new string[1];
                resp[0] = e.Id;
                var f = new Function("read_" + d.nodeId, d.name, "", resp, m, null);
                msbApplication.AddFunction(f);
            }
        }

        var writeNodes = ((Newtonsoft.Json.Linq.JArray)msbApplication.Configuration.Parameters["opcua.client.writeNodes"].Value).ToObject<List<WriteNode>>();

        if (writeNodes != null)
        {
            System.Reflection.MethodInfo m = null;

            foreach (var d in writeNodes)
            {
                if (d == null) continue;

                m = fitCbFunctionWithDatatype(d.nodeId);
                
                if (m == null) continue;

                var f = new Function("write_" + d.nodeId, d.name, "", m, null);
                msbApplication.AddFunction(f);
            }
        }

        var methods = ((Newtonsoft.Json.Linq.JArray)msbApplication.Configuration.Parameters["opcua.client.methods"].Value).ToObject<List<WriteNode>>();

        if (methods != null)
        {
            var m = typeof(Program).GetMethod("msbCallback_FunctionCallMethod");

            foreach (var d in methods)
            {
                if (d == null) continue;

                var f = new Function("method_" + d.nodeId, d.name, "", m, null);
                msbApplication.AddFunction(f);
            }
        }

        msbClient = new Fraunhofer.IPA.MSB.Client.Websocket.MsbClient(c.target_interface);
        msbClient.Connected += msbCallback_Connected;
        msbClient.ConfigurationParameterReceived += msbCallback_ConfigurationEvent;

        msbClient.ConnectAsync();

        run = true;

        while(run && !reconfigure)
        {
            System.Threading.Thread.Sleep(5000);
        }

        try {
            msbClient.Disconnect();
            opcUaClient.EndSession();
        } catch {

        }

        if (!run && reconfigure) goto Start;
    }
}