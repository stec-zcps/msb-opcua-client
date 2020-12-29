using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Linq;
using Opc.UaFx;
using Opc.UaFx.Client;

using Fraunhofer.IPA.MSB.Client.API.Attributes;
using Fraunhofer.IPA.MSB.Client.API.Configuration;
using Fraunhofer.IPA.MSB.Client.API.Model;

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

public class Server
{
    public string url = "", security = "", user = "", password = "";
}

public class Node
{
    public string nodeId, name;
    public bool handleAsComplexObject;
}

public class ReadNode : Node
{
    public ReadNode(){
        
    }
}

public class WriteNode : Node
{

}

public class MonitorNode : Node
{
    public string samplingInterval, queueSize;
}

//public Type GibeDatatyp()
    public class Program
    {
        public static void ConfigEventHandler(object sender, Fraunhofer.IPA.MSB.Client.API.EventArgs.ConfigurationParameterReceivedEventArgs e)
        {
            go = false;
        }

        public static void FunktionCb(FunctionCallInfo info)
        {
            if (client.State != OpcClientState.Connected && client.State != OpcClientState.Reconnected) return;

            var nodeId = info.Function.Id.Substring(("read_").Length);
            var s = nodeId.Split(";");
            var nodeId_ = new OpcNodeId(s[1].Substring(("s=").Length), int.Parse(s[0].Substring(("ns=").Length)));

            var val = client.ReadNode(nodeId_);

            if (val.Status.IsBad) return;
                
            var ev = new EventData(readEvents[nodeId]);
            ev.Value = val.Value;

            myMsbClient.PublishAsync(myMsbApplication, ev);
        }

        public static void functionWriteString(string val, FunctionCallInfo info)
        {
            functionWriteObject(val, info);
        }

        public static void functionWriteInt(int val, FunctionCallInfo info)
        {
            functionWriteObject(val, info);
        }

        public static void functionWriteUInt(uint val, FunctionCallInfo info)
        {
            functionWriteObject(val, info);
        }

        public static void functionWriteDouble(double val, FunctionCallInfo info)
        {
            functionWriteObject(val, info);
        }

        public static void functionWriteFloat(float val, FunctionCallInfo info)
        {
            functionWriteObject(val, info);
        }

        public static void functionWriteBoolean(bool val, FunctionCallInfo info)
        {
            functionWriteObject(val, info);
        }

        public static void functionWriteObject(object val, FunctionCallInfo info)
        {
            if (client.State != OpcClientState.Connected && client.State != OpcClientState.Reconnected) return;

            var nodeId = info.Function.Id.Substring(("read_").Length);
            var s = nodeId.Split(";");
            var nodeId_ = new OpcNodeId(s[1].Substring(("s=").Length), int.Parse(s[0].Substring(("ns=").Length)));
            var status = client.WriteNode(nodeId_, val);
        }

        private static void Browse(OpcNodeInfo node)
        {
            Program.Browse(node, 0);
        }

        private static void Browse(OpcNodeInfo node, int level)
        {
            //// In general attributes and children are retrieved from the server on demand. This
            //// is done to reduce the amount of traffic and to improve the preformance when
            //// searching/browsing for specific attributes or children. After attributes or
            //// children of a node were browsed they are stored internally so that subsequent
            //// attribute and children requests are processed without any interaction with the
            //// OPC UA server.

            // Browse the DisplayName attribute of the node. It is also possible to browse
            // multiple attributes at once (see the method Attributes(...)).
            var displayName = node.Attribute(OpcAttribute.DisplayName);

            Console.WriteLine(
                    "{0}{1} ({2})",
                    new string(' ', level * 4),
                    node.NodeId.ToString(OpcNodeIdFormat.Foundation),
                    displayName.Value);

            // Browse the children of the node and continue browsing in preorder.
            foreach (var childNode in node.Children())
                Program.Browse(childNode, level + 1);
        }

        public static OpcClient client;

        public static Type DatentypAusNodeId(OpcNodeId nodeId)
        {
            if (nodeId.NamespaceIndex != 0 || nodeId.Type != OpcNodeIdType.Numeric) return null;
            
            switch((uint)nodeId.Value)
            {    
                case 1: return typeof(Boolean);
                case 10: return typeof(float);
                case 11: return typeof(Double);
                case 12: return typeof(string);
                case 27: return typeof(int);
                case 28: return typeof(uint);
            }

            return null;
        }

        public static System.Reflection.MethodInfo CbFunktionNachDatentyp(string nodeId)
        {
            var s_ = nodeId.Split(";");
            var o = new OpcNodeId(s_[1].Substring(2), int.Parse(s_[0].Substring(3)));
            var nInfo = client.BrowseNode(o) as OpcVariableNodeInfo;

            if (nInfo == null) return null;

            var dt = DatentypAusNodeId(nInfo.DataTypeId);

            if (dt == null) return null;

            string ident = "";

            if (dt == typeof(int))
            {
                ident = "functionWriteInt";
            } else if (dt == typeof(uint))
            {
                ident = "functionWriteUInt";
            } else if (dt == typeof(bool))
            {
                ident = "functionWriteBoolean";
            } else if (dt == typeof(float))
            {
                ident = "functionWriteFloat";
            } else if (dt == typeof(double))
            {
                ident = "functionWriteDouble";
            } else if (dt == typeof(string))
            {
                ident = "functionWriteString";
            }

            var refl = typeof(Program).GetMethods();
            System.Reflection.MethodInfo m = null;
            foreach(var r in refl)
            {
                if (r.Name == ident)
                {
                    m = r;
                    break;
                }
            }

            return m;
        }

        public static Event EventAusNodeId(string nodeId)
        {
            var s_ = nodeId.Split(";");
            var o = new OpcNodeId(s_[1].Substring(2), int.Parse(s_[0].Substring(3)));
            var nInfo = client.BrowseNode(o) as OpcVariableNodeInfo;

            if (nInfo == null) return null;

            var dt = DatentypAusNodeId(nInfo.DataTypeId);

            if (dt == null) return null;

            return new Event(nodeId, nInfo.Name.Value, "", dt);
        }
        public static void HandleDataChanged(object sender, OpcDataChangeReceivedEventArgs e)
        {
            // The tag property contains the previously set value.
            var item = (OpcMonitoredItem)sender;
            var nodeId = "ns=" + item.NodeId.NamespaceIndex + ";s=" + item.NodeId.ValueAsString;

            var ev = new EventData(monitoredEvents[nodeId]);
            ev.Value = e.Item.Value.Value;

            myMsbClient.PublishAsync(myMsbApplication, ev);
        }

        public static Fraunhofer.IPA.MSB.Client.Websocket.MsbClient myMsbClient;

        public static Application myMsbApplication;

        public static Dictionary<string, Event> monitoredEvents, readEvents;

        public static Dictionary<String, ConfigurationParameterValue> ConfigParametersFromObject(string pfad, char trenner, object o)
        {
            var r = new Dictionary<String, ConfigurationParameterValue>();

            if (o == null) return r;

            var typ = o.GetType();

            if (typ == typeof(int) || typ == typeof(uint) || typ == typeof(string) || typ == typeof(bool) || typ == typeof(float) || typ == typeof(double))
            {
                r.Add(pfad, new ConfigurationParameterValue(o));
            } else if(typ.BaseType == typeof(System.Object) ) {
                if (typ.IsGenericType)
                {                    
                    var param = new ConfigurationParameterValue(o);
                    r.Add(pfad, param);                    
                } else {
                    var felder = typ.GetFields();
                    foreach(var feld in felder)
                    {
                        var r_sub = ConfigParametersFromObject(pfad + trenner + feld.Name, trenner, feld.GetValue(o));
                        foreach (var r_sub_ in r_sub) r.Add(r_sub_.Key, r_sub_.Value);
                    }
                }
            } else {

            }

            return r;
        }

        public static bool go;
        static void Main(string[] args)
        {
            Start:

            var c_file = System.IO.File.ReadAllText(args[0]);
            var c = Newtonsoft.Json.JsonConvert.DeserializeObject<Service>(c_file);            

            myMsbClient = new Fraunhofer.IPA.MSB.Client.Websocket.MsbClient(c.target_interface);
            myMsbClient.ConfigurationParameterReceived += ConfigEventHandler;
            var myMsbApplication = new Application(c.uuid, c.name, c.token, c.token);

            myMsbApplication.AddConfigurationParameter("opcua.server.url", new ConfigurationParameterValue(""));
            myMsbApplication.AddConfigurationParameter("opcua.server.security", new ConfigurationParameterValue(""));
            myMsbApplication.AddConfigurationParameter("opcua.server.user", new ConfigurationParameterValue(""));
            myMsbApplication.AddConfigurationParameter("opcua.server.password", new ConfigurationParameterValue(""));
            myMsbApplication.AddConfigurationParameter("opcua.client.readNodes", new ConfigurationParameterValue(new List<ReadNode>()));
            myMsbApplication.AddConfigurationParameter("opcua.client.writeNodes", new ConfigurationParameterValue(new List<WriteNode>()));
            myMsbApplication.AddConfigurationParameter("opcua.client.monitorNodes", new ConfigurationParameterValue(new List<MonitorNode>()));

            if (!System.IO.File.Exists(myMsbApplication.ConfigurationPersistencePath)) myMsbApplication.Configuration.SaveToFile(myMsbApplication.ConfigurationPersistencePath);
            myMsbApplication.AutoPersistConfiguration = true;

            var ev_uncon = new Event("OPCUA_No_Conn", "No connection to OPC UA Server", "", typeof(string));
            var ev_con = new Event("OPCUA_Conn", "Connection to OPC UA Server", "", typeof(string));
            myMsbApplication.AddEvent(ev_uncon);
            myMsbApplication.AddEvent(ev_con);

            try{
                client = new OpcClient((string)myMsbApplication.Configuration.Parameters["opcua.server.url"].Value);
                client.Connect();
            }
            catch
            {

            }

            var monitorNodes_linq = ((Newtonsoft.Json.Linq.JArray)myMsbApplication.Configuration.Parameters["opcua.client.monitorNodes"].Value);
            var monitorNodes = monitorNodes_linq.ToObject<List<MonitorNode>>();

            //if (opcUaConfiguration.monitoredNodes != null)
            if (monitorNodes != null)
            {
                var cm_intern = new List<OpcSubscribeDataChange>();
                monitoredEvents = new Dictionary<string, Event>();
                //foreach (var d in opcUaConfiguration.monitoredNodes)
                foreach (var d in monitorNodes)
                {
                    if (d == null) continue;

                    var e = EventAusNodeId(d.nodeId);
                    if (e != null)
                    {
                        e.Id = "monitored_" + e.Id;
                        monitoredEvents.Add(d.nodeId, e);
                        myMsbApplication.AddEvent(e);
                        cm_intern.Add(new OpcSubscribeDataChange(d.nodeId, HandleDataChanged));
                    }
                }

                if (cm_intern.Count > 0)
                {
                    var subscription = client.SubscribeNodes(cm_intern.ToArray());
                }
            }

            var readNodes_linq = ((Newtonsoft.Json.Linq.JArray)myMsbApplication.Configuration.Parameters["opcua.client.readNodes"].Value);
            var readNodes = monitorNodes_linq.ToObject<List<ReadNode>>();

            //if (opcUaConfiguration.readNodes != null)
            if (readNodes != null)
            {
                var refl = typeof(Program).GetMethods();
                System.Reflection.MethodInfo m = null;
                foreach(var r in refl)
                {
                    if (r.Name == "FunktionCb") m = r;
                }

                readEvents = new Dictionary<string, Event>();
                //foreach (var d in opcUaConfiguration.readNodes)
                foreach (var d in readNodes)
                {
                    if (d == null) continue;

                    var e = EventAusNodeId(d.nodeId);
                    e.Id = "read_" + e.Id;
                    e.Name = "Response - " + e.Name;
                    readEvents.Add(d.nodeId, e);
                    myMsbApplication.AddEvent(e);
                    var resp = new string[1];
                    resp[0] = e.Id;
                    var f = new Function("read_" + d.nodeId, d.name, "", resp, m, null);
                    myMsbApplication.AddFunction(f);
                }
            }

            var writeNodes_linq = ((Newtonsoft.Json.Linq.JArray)myMsbApplication.Configuration.Parameters["opcua.client.writeNodes"].Value);
            var writeNodes = writeNodes_linq.ToObject<List<WriteNode>>();

            //if (opcUaConfiguration.writeNodes != null)
            if (writeNodes != null)
            {
                var refl = typeof(Program).GetMethods();
                System.Reflection.MethodInfo m = null;

                foreach(var r in refl)
                {
                    if (r.Name == "functionWriteObject") m = r;
                }

                //foreach (var d in opcUaConfiguration.writeNodes)
                foreach (var d in writeNodes)
                {
                    if (d == null) continue;

                    m = CbFunktionNachDatentyp(d.nodeId);
                    if (m == null) continue;
                    var f = new Function("write_" + d.nodeId, d.name, "", m, null);
                    myMsbApplication.AddFunction(f);
                }
            }

            myMsbClient.ConnectAsync().Wait();

            myMsbClient.RegisterAsync(myMsbApplication).Wait();

            go = true;

            while(go)
            {
                System.Threading.Thread.Sleep(5000);
            }

            try {
                myMsbClient.Disconnect();
            } catch {

            }

            try {
                client.Disconnect();
            } catch {

            }

            goto Start;
        }
    }