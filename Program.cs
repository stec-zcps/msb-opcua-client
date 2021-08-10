using System;
using System.Collections.Generic;
using Fraunhofer.IPA.MSB.Client.API.Model;
using Fraunhofer.IPA.MSB.Clients.OPCUA;
using Serilog;

public class Program
{
    public static string connInfo(string hostname)
    {
        System.Net.IPEndPoint endPoint;
        using (System.Net.Sockets.Socket socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, 0))
        {
            string adr = hostname;
            if (adr.Contains("//"))
                adr = adr.Substring(adr.IndexOf("//")+2);
            if (adr.Contains('/'))
                adr = adr.Substring(0, adr.IndexOf('/'));

            try {
                socket.Connect(adr, 65530);
            } catch (Exception e)
            {
                Log.Error(e.Message);
            }
            
            endPoint = socket.LocalEndPoint as System.Net.IPEndPoint;
        }

        var strHostName = System.Net.Dns.GetHostName();
        var ipEntry = System.Net.Dns.GetHostEntry(strHostName);
        var addr = ipEntry.AddressList;
        string antw = "";

        foreach (var a in addr)
        {
            string a_ = a.ToString();
            if (a.Equals(endPoint.Address))
            {
                a_ += " (Verbindung zu MSB)";
            }
            antw += (a_ + "\\n");
        }

        return antw;
    }

    public static void msbCallback_Connected(object sender, System.EventArgs e)
    {
        Log.Logger.Error("Connected");
        msbClient.RegisterAsync(msbApplication);        
    }

    public static void msbCallback_Disconnected(object sender, System.EventArgs e)
    {
        msbActive = false;
        Log.Logger.Error("Disconnected");
    }

    public static void msbCallback_Registered(object sender, System.EventArgs e)
    {
        msbActive = true;
        Log.Logger.Error("Registered");
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

        msbClient.PublishAsyncForget(msbApplication, eventData);
    }

    public static void msbCallback_FunctionCallMethod(FunctionCallInfo info)
    {
        var nodeId = info.Function.Id.Substring(("method_").Length);
        var objNodeId = methodObjektNodeIds[nodeId];

        var res = opcUaClient.callMethod(objNodeId, nodeId);

        if (methodOutputTypes.ContainsKey(nodeId))
        {
            var output = Helper.CreateNewObject(methodOutputTypes[nodeId]);
            var felder_output = methodOutputTypes[nodeId].GetFields();
            for(int i = 0; i < felder_output.Length; ++i)
            {
                felder_output[i].SetValue(output, res[i]);
            }

            EventData eventData;
            eventData = new EventData(methodEvents[nodeId]) {
                CorrelationId = info.CorrelationId,
                Value = output
            };

            msbClient.PublishAsyncForget(msbApplication, eventData);
        }
    }
    public static void msbCallback_FunctionCallMethod_Generic<T>([Fraunhofer.IPA.MSB.Client.API.Attributes.MsbFunctionParameter(Name = "val")]T val, FunctionCallInfo info)
    {
        var nodeId = info.Function.Id.Substring(("method_").Length);
        var objNodeId = methodObjektNodeIds[nodeId];

        var t = methodInputTypes[nodeId];

        var felder = t.GetFields();
        var felder_T = typeof(T).GetFields();

        if (felder.Length != felder_T.Length) return;

        object[] args = new object[felder.Length];

        for(int i = 0; i < felder.Length; ++i)
        {
            args[i] = felder[i].GetValue(val);
        }
        
        var res = opcUaClient.callMethod(objNodeId, nodeId, args);

        if (methodOutputTypes.ContainsKey(nodeId))
        {
            var output = Helper.CreateNewObject(methodOutputTypes[nodeId]);
            var felder_output = methodOutputTypes[nodeId].GetFields();
            for(int i = 0; i < felder_output.Length; ++i)
            {
                felder_output[i].SetValue(output, res[i]);
            }

            EventData eventData;
            eventData = new EventData(methodEvents[nodeId]) {
                CorrelationId = info.CorrelationId,
                Value = output
            };

            msbClient.PublishAsyncForget(msbApplication, eventData);
        }
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
                msbClient.PublishAsyncForget(msbApplication, ev);
            } catch (Exception ex){
                Log.Error(ex.Message);
            }
        }
    }

    public static Fraunhofer.IPA.MSB.Client.Websocket.MsbClient msbClient;
    public static Application msbApplication;
    public static OpcUaClient opcUaClient;

    public static Dictionary<string, Event> monitoredEvents, readEvents, methodEvents;
    public static Dictionary<string, Opc.Ua.NodeId> methodObjektNodeIds;
    public static Dictionary<string, Type> methodInputTypes, methodOutputTypes;

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
        
        try
        {
            msbClient.PublishAsyncForget(msbApplication, resp);
        } catch (Exception ex){
            Log.Error(ex.Message);
        }
    }

    public static void opcuaCallback_Disconnected(object sender, System.EventArgs e)
    {
        if (!msbClient.IsConnected()) return;

        var resp = new EventData(msbEvent_OpcUaDisconnected);
        
        try
        {
            msbClient.PublishAsyncForget(msbApplication, resp);
        } catch (Exception ex){
            Log.Error(ex.Message);
        }
    }

    static Event msbEvent_OpcUaConnected, msbEvent_OpcUaDisconnected, msbEvent_OpcUaError;

    static void addStandardConfigurationParameters()
    {
        msbApplication.AddConfigurationParameter("opcua.server.url", new ConfigurationParameterValue(""));
        msbApplication.AddConfigurationParameter("opcua.server.security", new ConfigurationParameterValue(""));
        msbApplication.AddConfigurationParameter("opcua.server.user", new ConfigurationParameterValue(""));
        msbApplication.AddConfigurationParameter("opcua.server.password", new ConfigurationParameterValue(""));
        msbApplication.AddConfigurationParameter("opcua.subscription.publishingInterval", new ConfigurationParameterValue((Int32)1000));
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

    public static bool run, reconfigure, msbActive;
    static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:yyyy-MM-dd - HH:mm:ss}] [{SourceContext:s}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

        if (args.Length != 1)
        {
            Log.Error("Path to service description file required as first argument.");
            return;
        }

        if (!System.IO.File.Exists(args[0]))
        {
            Log.Error("Service description file does not exist. Creating template with new UUID. Exiting afterwards.");
            var s = new Service()
            {
                class_ = "Application",
                uuid = System.Guid.NewGuid().ToString(),
                name = "OPCUA-Websocket-Client",
                description = "OPCUA-Websocket-Client",
                token = System.Guid.NewGuid().ToString(),
                target_interface = "http://ws.msb.dia.cell.vfk.fraunhofer.de",
                log_to_console = true,
                log_to_file = false,
                logfile = "log.txt",
                logfile_maxsize = 10000000,
                logfile_maxfiles = 5
            };
            
            System.IO.File.WriteAllText(args[0].ToString(), Newtonsoft.Json.JsonConvert.SerializeObject(s));

            return;
        }

        Start:

        Service c;

        try
        {
            var c_file = System.IO.File.ReadAllText(args[0]);
            c = Newtonsoft.Json.JsonConvert.DeserializeObject<Service>(c_file);
        } catch (Exception e)
        {
            Log.Error("Service generation failed:" + e.Message);
            return;
        }        

        if (c.log_to_console && c.log_to_file)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:yyyy-MM-dd - HH:mm:ss}] [{SourceContext:s}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(c.logfile, outputTemplate:
                    "[{Timestamp:yyyy-MM-dd - HH:mm:ss}] [{SourceContext:s}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: (c.logfile_maxsize > 0 ? c.logfile_maxsize : 10000000),
                    retainedFileCountLimit: (c.logfile_maxfiles > 0 ? c.logfile_maxfiles : 1))
                .CreateLogger();
        } else if (c.log_to_file) {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(c.logfile, outputTemplate:
                    "[{Timestamp:yyyy-MM-dd - HH:mm:ss}] [{SourceContext:s}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: (c.logfile_maxsize > 0 ? c.logfile_maxsize : 10000000),
                    retainedFileCountLimit: (c.logfile_maxfiles > 0 ? c.logfile_maxfiles : 1))
                .CreateLogger();
        } else if (c.log_to_console) {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:yyyy-MM-dd - HH:mm:ss}] [{SourceContext:s}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        } else {
            Log.Logger = new LoggerConfiguration().CreateLogger();
        }
        
        var info = connInfo(c.target_interface);

        msbApplication = new Application(c.uuid, c.name, c.description + "\\n\\nNetzwerkinformationen:\\n" + info, c.token);
        msbApplication.AutoPersistConfiguration = true;

        if (!System.IO.File.Exists(msbApplication.ConfigurationPersistencePath))
        {
            addStandardConfigurationParameters();
            msbApplication.Configuration.SaveToFile(msbApplication.ConfigurationPersistencePath);
            msbApplication.Configuration.LoadFromFile(msbApplication.ConfigurationPersistencePath);
        } else {

        }

        addStandardEvents();

        try {
            opcUaClient = new OpcUaClient((string)msbApplication.Configuration.Parameters["opcua.server.url"].Value, c.name);
            opcUaClient.CreateSession((string)msbApplication.Configuration.Parameters["opcua.server.user"].Value, (string)msbApplication.Configuration.Parameters["opcua.server.password"].Value);
        } catch (Exception e)
        {
            Log.Error(e.Message);
        }

        var monitorNodes = ((Newtonsoft.Json.Linq.JArray)msbApplication.Configuration.Parameters["opcua.client.monitorNodes"].Value).ToObject<List<MonitorNode>>();

        if (monitorNodes != null)
        {
            monitoredEvents = new Dictionary<string, Event>();

            if (opcUaClient != null)
            {
                opcUaClient.generateSubscription((Int32)msbApplication.Configuration.Parameters["opcua.subscription.publishingInterval"].Value);          
            }
            
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

            opcUaClient.applySubcription();
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

        var methods = ((Newtonsoft.Json.Linq.JArray)msbApplication.Configuration.Parameters["opcua.client.methods"].Value).ToObject<List<Method>>();

        if (methods != null)
        {
            System.Reflection.MethodInfo m;

            methodEvents = new Dictionary<string, Event>();
            methodObjektNodeIds = new Dictionary<string, Opc.Ua.NodeId>();
            methodOutputTypes = new Dictionary<string, Type>();
            methodInputTypes = new Dictionary<string, Type>();

            foreach (var d in methods)
            {
                if (d == null) continue;

                methodObjektNodeIds.Add(d.methodNodeId, Opc.Ua.NodeId.Parse(d.objectNodeId));

                m = typeof(Program).GetMethod("msbCallback_FunctionCallMethod");
                string[] respEvents = null;
                
                Type input, output;
                if (opcUaClient.getInputOutputParameters(d.methodNodeId, out input, out output))
                {
                    if (input != null)
                    {
                        m = typeof(Program).GetMethod("msbCallback_FunctionCallMethod_Generic").MakeGenericMethod(input);
                        methodInputTypes.Add(d.methodNodeId, input);
                    } else {
                        
                    }

                    if (output != null)
                    {
                        methodOutputTypes.Add(d.methodNodeId, output);
                        var e = new Event("method_" + d.methodNodeId, "Response - " + d.name, "Method response " + d.name, output);
                        respEvents = new string[1];
                        respEvents[0] = e.Id;
                        methodEvents.Add(d.methodNodeId, e);
                        msbApplication.AddEvent(e);
                    }
                }

                Function f;

                if (respEvents != null)
                {
                    f = new Function("method_" + d.methodNodeId, d.name, "Method " + d.name, respEvents, m, null); 
                } else {
                    f = new Function("method_" + d.methodNodeId, d.name, "", m, null);
                }

                msbApplication.AddFunction(f);
            }
        }

        msbClient = new Fraunhofer.IPA.MSB.Client.Websocket.MsbClient(c.target_interface);

        msbClient.Connected += msbCallback_Connected;
        msbClient.Disconnected += msbCallback_Disconnected;
        msbClient.Registered += msbCallback_Registered;
        msbClient.ConfigurationParameterReceived += msbCallback_ConfigurationEvent;

        msbClient.EventCacheSizePerService = 3;

        msbClient.AutoReconnect = true;
        msbClient.AutoReconnectIntervalInMilliseconds = 10000;
        msbActive = false;

        msbClient.ConnectAsync();

        run = true;

        while(run && !reconfigure)
        {
            System.Threading.Thread.Sleep(5000);
        }

        try {
            msbClient.Disconnect();
        } catch (Exception ex) {
            Log.Error(ex.Message);
        }

        try {
            opcUaClient.EndSession();
        } catch (Exception ex) {
            Log.Error(ex.Message);
        }

        if (!run && reconfigure) goto Start;
    }
}