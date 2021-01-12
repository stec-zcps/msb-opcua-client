using System;

public class OpcUaClient
{
    private Opc.Ua.Configuration.ApplicationInstance application;
    private Opc.Ua.EndpointDescription selectedEndpoint;
    
    public OpcUaClient(string server, string name)
    {
        application = new Opc.Ua.Configuration.ApplicationInstance
        {
            ApplicationName = name,
            ApplicationType = Opc.Ua.ApplicationType.Client
        };

        application.ApplicationConfiguration = new Opc.Ua.ApplicationConfiguration(){
            ApplicationName = name,
            ApplicationType = Opc.Ua.ApplicationType.Client,
            ApplicationUri = "urn:localhost:071:MsbClient",
            SecurityConfiguration = new Opc.Ua.SecurityConfiguration {
                ApplicationCertificate = new Opc.Ua.CertificateIdentifier { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\071\CertificateStores\MachineDefault", SubjectName="MsbClient" },
                TrustedIssuerCertificates = new Opc.Ua.CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\071\CertificateStores\UA Certificate Authorities" },
                TrustedPeerCertificates = new Opc.Ua.CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\071\CertificateStores\UA Applications" },
                RejectedCertificateStore = new Opc.Ua.CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\071\CertificateStores\RejectedCertificates" },
                AutoAcceptUntrustedCertificates = true
            },
            TransportConfigurations = new Opc.Ua.TransportConfigurationCollection(),
            TransportQuotas = new Opc.Ua.TransportQuotas { OperationTimeout = 15000 },
            ClientConfiguration = new Opc.Ua.ClientConfiguration { DefaultSessionTimeout = 60000 },
            TraceConfiguration = new Opc.Ua.TraceConfiguration()
        };
        //application.ApplicationConfiguration.Validate(Opc.Ua.ApplicationType.Client).GetAwaiter().GetResult();
        if (application.ApplicationConfiguration.SecurityConfiguration.AutoAcceptUntrustedCertificates)
        {
            application.ApplicationConfiguration.CertificateValidator.CertificateValidation += (s, e) => { e.Accept = (e.Error.StatusCode == Opc.Ua.StatusCodes.BadCertificateUntrusted); };
        }

        selectedEndpoint = Opc.Ua.Client.CoreClientUtils.SelectEndpoint(server, false);
    }

    public void CreateSession()
    {
        var endpointConfiguration = Opc.Ua.EndpointConfiguration.Create(application.ApplicationConfiguration);
        var endpoint = new Opc.Ua.ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);
        session = Opc.Ua.Client.Session.Create(application.ApplicationConfiguration, endpoint, false, application.ApplicationName, 60000, new Opc.Ua.UserIdentity(new Opc.Ua.AnonymousIdentityToken()), null).Result;
        session.KeepAlive += Callback_KeepAlive;
        subscription = new Opc.Ua.Client.Subscription(session.DefaultSubscription) { PublishingInterval = 100 };
    }

    public void EndSession()
    {
        session.Close();
        session.Dispose();
    }

    public static Type TranslateNodeIdtoDatatype(Opc.Ua.NodeId nodeId)
    {
        if (nodeId.NamespaceIndex != 0 || nodeId.IdType != Opc.Ua.IdType.Numeric) return null;
        
        switch((uint)nodeId.Identifier)
        {    
            case 1: return typeof(Boolean);

            case 10: return typeof(float);
            case 11: return typeof(Double);

            case 12: return typeof(string);

            case 3: return typeof(byte);
            case 5: return typeof(UInt16);
            case 7: return typeof(UInt32);
            case 9: return typeof(UInt64);            
            
            case 2: return typeof(sbyte);
            case 4: return typeof(Int16);
            case 6: return typeof(Int32);
            case 8: return typeof(Int64);
        }

        return null;
    }
    
    public event EventHandler<EventArgs> Connected;
    public event EventHandler<EventArgs> Disconnected;
    Opc.Ua.Client.SessionReconnectHandler reconnectHandler;
    Opc.Ua.Client.Session session;
    Opc.Ua.Client.Subscription subscription;

    public void addAndCreateSubscription()
    {
        session.AddSubscription(subscription);
        subscription.Create();
    }

    public void monitorItem(string nodeId, Opc.Ua.Client.MonitoredItemNotificationEventHandler handler)
    {
        var item = new Opc.Ua.Client.MonitoredItem(subscription.DefaultItem)
                            {
                                RelativePath = "",
                                StartNodeId = Opc.Ua.NodeId.Parse(nodeId)
                            };
        item.Notification += handler;
        subscription.AddItem(item);
    }

    public bool readToNode(string nodeId, out Opc.Ua.Node node)
    {
        node = null;

        if (!session.Connected)
        {
            return false;
        }
        
        var nodeId_ = Opc.Ua.NodeId.Parse(nodeId);

        try {
            node = session.ReadNode(nodeId_);
        } catch {

        }        

        return node != null;
    }

    public bool readToObject(string nodeId, out object value)
    {
        if (!session.Connected)
        {
            value = null;
            return false;
        }
        
        var nodeId_ = Opc.Ua.NodeId.Parse(nodeId);

        var val = session.ReadValue(nodeId_);

        if (val.StatusCode == Opc.Ua.StatusCodes.Good)
        {
            value = val.Value;
            return true;
        }

        value = null;
        return false;
    }

    public bool writeNode(string nodeId, object value)
    {
        if (!session.Connected) return false;

        var nodeId_ = Opc.Ua.NodeId.Parse(nodeId);
        var coll = new Opc.Ua.WriteValueCollection() {
            new Opc.Ua.WriteValue() {
                NodeId = nodeId_,
                Value = new Opc.Ua.DataValue()
                {
                    Value = value
                }
            }
        };

        var stat = new Opc.Ua.StatusCodeCollection();
        var diag = new Opc.Ua.DiagnosticInfoCollection();
        var rsp = session.Write(null, coll, out stat, out diag);

        return rsp.ServiceResult == Opc.Ua.StatusCodes.Good;
    }

    public void callMethod(string nodeId)
    {
        if (!session.Connected) return;

        var nodeId_ = Opc.Ua.NodeId.Parse(nodeId);

        session.Call(nodeId_, nodeId_);
    }
    
    private void Callback_KeepAlive(Opc.Ua.Client.Session sender, Opc.Ua.Client.KeepAliveEventArgs e)
    {
        if (e.Status != null && Opc.Ua.ServiceResult.IsNotGood(e.Status))
        {
            if (Disconnected != null) Disconnected.Invoke(sender, e);

            if (reconnectHandler == null)
            {
                reconnectHandler = new Opc.Ua.Client.SessionReconnectHandler();
                reconnectHandler.BeginReconnect(sender, 5000, Callback_Reconnect);
            }
        }
    }

    private void Callback_Reconnect(object sender, EventArgs e)
    {
        // ignore callbacks from discarded objects.
        if (!Object.ReferenceEquals(sender, reconnectHandler)) return;

        session = reconnectHandler.Session;
        reconnectHandler.Dispose();
        reconnectHandler = null;

        if (Connected != null) Connected.Invoke(sender, e);
    }
}