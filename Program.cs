using System;
using System.Collections.Generic;
using Fraunhofer.IPA.MSB.Client.API.Model;

//public Type GibeDatatyp()
    public class Program
    {
        public static void ConnectedHandler(object sender, System.EventArgs e)
        {
            myMsbClient.RegisterAsync(myMsbApplication);
        }  

        public static void ConfigEventHandler(object sender, Fraunhofer.IPA.MSB.Client.API.EventArgs.ConfigurationParameterReceivedEventArgs e)
        {
            reconfigure = true;
        }        

        public static void FunktionCb(FunctionCallInfo info)
        {            
            var nodeId = info.Function.Id.Substring(("read_").Length);
            object val;
            EventData eventData;

            if(client.readValue(nodeId, out val))
            {
                eventData = new EventData(readEvents[nodeId]) {
                    CorrelationId = info.CorrelationId,
                    Value = val
                };
            } else {
                eventData = new EventData(ev_err) {
                    CorrelationId = info.CorrelationId
                };
            }
                        
            myMsbClient.PublishAsync(myMsbApplication, eventData);            
        }

        public static void FunktionMethod(List<object> parameter, FunctionCallInfo info)
        {
            var nodeId = info.Function.Id.Substring(("method_").Length);

            client.callMethod(nodeId);
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
            var nodeId = info.Function.Id.Substring(("read_").Length);
            
            client.write(nodeId, val);
        }

        public static System.Reflection.MethodInfo CbFunktionNachDatentyp(string nodeId)
        {
            Opc.Ua.Node node;
            
            if (!client.read(nodeId, out node)) return null;

            if (node.GetType() != typeof(Opc.Ua.VariableNode)) return null;

            var dt = OpcUaClient.DatentypAusNodeId(((Opc.Ua.VariableNode)node).DataType);

            if (dt == null) return null;

            string ident = "";

            if (dt == typeof(int)) ident = "functionWriteInt";
            else if (dt == typeof(uint)) ident = "functionWriteUInt";
            else if (dt == typeof(bool)) ident = "functionWriteBoolean";
            else if (dt == typeof(float)) ident = "functionWriteFloat";
            else if (dt == typeof(double)) ident = "functionWriteDouble";
            else if (dt == typeof(string)) ident = "functionWriteString";
            else return null;

            return typeof(Program).GetMethod(ident);
        }

        public static Event EventAusNodeId(string nodeId)
        {
            Opc.Ua.Node node;
            
            if (!client.read(nodeId, out node)) return null;

            if (node.GetType() != typeof(Opc.Ua.VariableNode)) return null;

            var dt = OpcUaClient.DatentypAusNodeId(((Opc.Ua.VariableNode)node).DataType);

            if (dt == null) return null;

            return new Event(nodeId, node.DisplayName + "(" + node.BrowseName + ")", "", dt);
        }

        private static void OnNotification(Opc.Ua.Client.MonitoredItem item, Opc.Ua.Client.MonitoredItemNotificationEventArgs e)
        {
            string id = item.ResolvedNodeId.ToString();

            var me = monitoredEvents[id];

            foreach (var value in item.DequeueValues())
            {
                var ev = new EventData(me);
                ev.Value = value.Value;
                try
                {
                    myMsbClient.PublishAsync(myMsbApplication, ev);
                } catch {

                }                
            }
        }

        public static Fraunhofer.IPA.MSB.Client.Websocket.MsbClient myMsbClient;

        public static Application myMsbApplication;

        public static OpcUaClient client;

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

        public static void OpcUaConnectedHandler(object sender, System.EventArgs e)
        {
            if (!myMsbClient.IsConnected()) return;

            var resp = new EventData(ev_con);
            myMsbClient.PublishAsync(myMsbApplication, resp);
        }

        public static void OpcUaDisconnectedHandler(object sender, System.EventArgs e)
        {
            if (!myMsbClient.IsConnected()) return;

            var resp = new EventData(ev_uncon);
            myMsbClient.PublishAsync(myMsbApplication, resp);
        }

        static Event ev_con, ev_uncon, ev_err;

        static void addStandardConfigurationParameters()
        {
            myMsbApplication.AddConfigurationParameter("opcua.server.url", new ConfigurationParameterValue(""));
            myMsbApplication.AddConfigurationParameter("opcua.server.security", new ConfigurationParameterValue(""));
            myMsbApplication.AddConfigurationParameter("opcua.server.user", new ConfigurationParameterValue(""));
            myMsbApplication.AddConfigurationParameter("opcua.server.password", new ConfigurationParameterValue(""));
            myMsbApplication.AddConfigurationParameter("opcua.client.readNodes", new ConfigurationParameterValue(new List<ReadNode>()));
            myMsbApplication.AddConfigurationParameter("opcua.client.writeNodes", new ConfigurationParameterValue(new List<WriteNode>()));
            myMsbApplication.AddConfigurationParameter("opcua.client.monitorNodes", new ConfigurationParameterValue(new List<MonitorNode>()));
            myMsbApplication.AddConfigurationParameter("opcua.client.methods", new ConfigurationParameterValue(new List<Method>()));
        }

        static void addStandardEvents()
        {
            ev_uncon = new Event("OPCUA_No_Conn", "No connection to OPC UA Server", "", typeof(string));
            ev_con = new Event("OPCUA_Conn", "Connection to OPC UA Server", "", typeof(string));
            ev_err = new Event("OPCUA_Error", "OPC UA Error", "", typeof(string));
            myMsbApplication.AddEvent(ev_uncon);
            myMsbApplication.AddEvent(ev_con);
            myMsbApplication.AddEvent(ev_err);
        }

        public static bool run, reconfigure;
        static void Main(string[] args)
        {
            Start:

            var c_file = System.IO.File.ReadAllText(args[0]);
            var c = Newtonsoft.Json.JsonConvert.DeserializeObject<Service>(c_file);

            myMsbApplication = new Application(c.uuid, c.name, c.token, c.token);

            if (!System.IO.File.Exists(myMsbApplication.ConfigurationPersistencePath))
            {
                addStandardConfigurationParameters();
                myMsbApplication.Configuration.SaveToFile(myMsbApplication.ConfigurationPersistencePath);
            } else {

            }

            myMsbApplication.AutoPersistConfiguration = true;

            addStandardEvents();

            client = new OpcUaClient((string)myMsbApplication.Configuration.Parameters["opcua.server.url"].Value, c.name);
            client.CreateSession();
        
            var monitorNodes = ((Newtonsoft.Json.Linq.JArray)myMsbApplication.Configuration.Parameters["opcua.client.monitorNodes"].Value).ToObject<List<MonitorNode>>();

            if (monitorNodes != null)
            {
                monitoredEvents = new Dictionary<string, Event>();
                
                foreach (var d in monitorNodes)
                {
                    if (d == null) continue;

                    var e = EventAusNodeId(d.nodeId);

                    if (e == null) continue;

                    e.Id = "monitored_" + e.Id;
                    monitoredEvents.Add(d.nodeId, e);
                    myMsbApplication.AddEvent(e);

                    client.monitorItem(d.nodeId, OnNotification);               
                }

                client.andAndCreateSubscription();
            }

            var readNodes = ((Newtonsoft.Json.Linq.JArray)myMsbApplication.Configuration.Parameters["opcua.client.readNodes"].Value).ToObject<List<ReadNode>>();

            if (readNodes != null)
            {
                System.Reflection.MethodInfo m = typeof(Program).GetMethod("FunktionCb");
                
                readEvents = new Dictionary<string, Event>();

                foreach (var d in readNodes)
                {
                    if (d == null) continue;

                    var e = EventAusNodeId(d.nodeId);

                    if (e == null) continue;

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

            var writeNodes = ((Newtonsoft.Json.Linq.JArray)myMsbApplication.Configuration.Parameters["opcua.client.writeNodes"].Value).ToObject<List<WriteNode>>();

            if (writeNodes != null)
            {
                System.Reflection.MethodInfo m = null;

                foreach (var d in writeNodes)
                {
                    if (d == null) continue;

                    m = CbFunktionNachDatentyp(d.nodeId);
                    
                    if (m == null) continue;

                    var f = new Function("write_" + d.nodeId, d.name, "", m, null);
                    myMsbApplication.AddFunction(f);
                }
            }

            var methods = ((Newtonsoft.Json.Linq.JArray)myMsbApplication.Configuration.Parameters["opcua.client.methods"].Value).ToObject<List<WriteNode>>();

            if (methods != null)
            {
                System.Reflection.MethodInfo m = typeof(Program).GetMethod("FunktionMethod");

                foreach (var d in methods)
                {
                    if (d == null) continue;

                    var f = new Function("method_" + d.nodeId, d.name, "", m, null);
                    myMsbApplication.AddFunction(f);
                }
            }

            myMsbClient = new Fraunhofer.IPA.MSB.Client.Websocket.MsbClient(c.target_interface);
            myMsbClient.Connected += ConnectedHandler;
            myMsbClient.ConfigurationParameterReceived += ConfigEventHandler;

            myMsbClient.ConnectAsync();

            run = true;

            while(run && !reconfigure)
            {
                System.Threading.Thread.Sleep(5000);
            }

            try {
                myMsbClient.Disconnect();
                client.EndSession();
            } catch {

            }

            if (!run && reconfigure) goto Start;
        }
    }