//<summary>
//  Title   : Configured server
//  System  : Microsoft Visual C# .NET 2008
//  $LastChangedDate$
//  $Rev$
//  $LastChangedBy$
//  $URL$
//  $Id$
//
//  Copyright (C)2009, CAS LODZ POLAND.
//  TEL: +48 (42) 686 25 47
//  mailto://techsupp@cas.eu
//  http://www.cas.eu
//</summary>

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Opc.Ua;

namespace CAS.OPC.UA.Viewer.Client.Controls
{
    /// <summary>
    /// Prompts the user to edit a ComPseudoServerDlg.
    /// </summary>
    public partial class ConfiguredServerDlg : Form
    {
        #region Constructors
        /// <summary>
        /// Initializes the dialog.
        /// </summary>
        public ConfiguredServerDlg()
        {
            InitializeComponent();
            
            // options for override limits are fixed.
            foreach (UseDefaultLimits value in Enum.GetValues(typeof(UseDefaultLimits)))
            {
                UseDefaultLimitsCB.Items.Add(value);
            }

            m_userIdentities = new Dictionary<UserTokenType,UserIdentityToken>();
        }
        #endregion

        #region Private Fields
        /// <summary>
        /// The possible encodings.
        /// </summary>
        private enum Encoding
        {
            Default,
            Xml,
            Binary
        }

        /// <summary>
        /// The possible COM identities.
        /// </summary>
        private enum ComIdentityType
        {
            None = -1,
            DA = (int)ComSpecification.DA,
            AE = (int)ComSpecification.AE,
            HDA = (int)ComSpecification.HDA,
        }

        /// <summary>
        /// Whether to override limits
        /// </summary>
        private enum UseDefaultLimits
        {
            Yes,
            No
        }

        private ConfiguredEndpoint m_endpoint;
        private EndpointDescription m_currentDescription;
        private EndpointDescriptionCollection m_availableEndpoints;
        private int m_discoveryTimeout;
        private int m_discoverCount;
        private ApplicationConfiguration m_configuration;
        private bool m_updating;
        private Dictionary<UserTokenType,UserIdentityToken> m_userIdentities;
        private EndpointComIdentity m_comIdentity;
        private EndpointConfiguration m_endpointConfiguration;
        private bool m_discoverySucceeded;
        private Uri m_discoveryUrl;
        private bool m_showAllOptions;
        #endregion

        #region Public Interface
        /// <summary>
        /// The timeout in milliseconds to use when discovering servers.
        /// </summary>
        [System.ComponentModel.DefaultValue(20000)]
        public int DiscoveryTimeout
        {
            get { return m_discoveryTimeout; }
            set { Interlocked.Exchange(ref m_discoveryTimeout, value); }
        }
        /// <summary>
        /// Displays the dialog.
        /// </summary>
        public ConfiguredEndpoint ShowDialog(ApplicationDescription server, ApplicationConfiguration configuration)
        {
            if (server == null) throw new ArgumentNullException("server");

            m_configuration = configuration;

            // construct a list of available endpoint descriptions for the application.
            m_availableEndpoints = new EndpointDescriptionCollection();
            m_endpointConfiguration = EndpointConfiguration.Create(configuration);

            // create a default endpoint description.
            m_endpoint = null;
            m_currentDescription = null;

            // initializing the protocol will trigger an update to all other controls.
            InitializeProtocols(m_availableEndpoints);

            UseDefaultLimitsCB.SelectedItem = UseDefaultLimits.Yes;

            // discover endpoints in the background.
            m_discoverySucceeded = false;
            Interlocked.Increment(ref m_discoverCount);
            ThreadPool.QueueUserWorkItem(new WaitCallback(OnDiscoverEndpoints), server);

            if (ShowDialog() != DialogResult.OK)
            {
                return null;
            }
                        
            return m_endpoint;      
        }

        /// <summary>
        /// Displays the dialog.
        /// </summary>
        public ConfiguredEndpoint ShowDialog(ConfiguredEndpoint endpoint, ApplicationConfiguration configuration)
        {
            if (endpoint == null) throw new ArgumentNullException("endpoint");

            m_endpoint = endpoint;
            m_configuration = configuration;

            // construct a list of available endpoint descriptions for the application.
            m_availableEndpoints = new EndpointDescriptionCollection();

            m_availableEndpoints.Add(endpoint.Description);
            m_currentDescription = endpoint.Description;
            m_endpointConfiguration = endpoint.Configuration;

            if (m_endpointConfiguration == null)
            {
                m_endpointConfiguration = EndpointConfiguration.Create(configuration);
            }

            if (endpoint.Collection != null)
            {
                foreach (ConfiguredEndpoint existingEndpoint in endpoint.Collection.Endpoints)
                {
                    if (existingEndpoint.Description.Server.ApplicationUri == endpoint.Description.Server.ApplicationUri)
                    {
                        m_availableEndpoints.Add(existingEndpoint.Description);
                    }
                }
            }

            // copy user identity.
            if (endpoint.UserIdentity != null)
            {
                UserTokenType tokenType = UserTokenType.Anonymous;

                if (endpoint.UserIdentity is UserNameIdentityToken)
                {
                    tokenType = UserTokenType.UserName;
                }
                else if (endpoint.UserIdentity is X509IdentityToken)
                {
                    tokenType = UserTokenType.Certificate;
                }
                else if (endpoint.UserIdentity is IssuedIdentityToken)
                {
                    tokenType = UserTokenType.IssuedToken;
                }                
               
                m_userIdentities[tokenType] = endpoint.UserIdentity;
            }
            
            // copy com identity.
            m_comIdentity = endpoint.ComIdentity;

            // initializing the protocol will trigger an update to all other controls.
            InitializeProtocols(m_availableEndpoints);

            // check if the current settings match the defaults.
            EndpointConfiguration defaultConfiguration = EndpointConfiguration.Create(configuration);

            if (SameAsDefaults(defaultConfiguration, m_endpoint.Configuration))
            {
                UseDefaultLimitsCB.SelectedItem = UseDefaultLimits.Yes;
            }
            else
            {
                UseDefaultLimitsCB.SelectedItem = UseDefaultLimits.No;
            }
                        
            // discover endpoints in the background.
            Interlocked.Increment(ref m_discoverCount);
            ThreadPool.QueueUserWorkItem(new WaitCallback(OnDiscoverEndpoints), m_endpoint.Description.Server);

            if (ShowDialog() != DialogResult.OK)
            {
                return null;
            }
                        
            return m_endpoint;                
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Returns true if the configuration is the same as the default.
        /// </summary>
        private bool SameAsDefaults(EndpointConfiguration defaultConfiguration, EndpointConfiguration currentConfiguration)
        {
            if (defaultConfiguration.ChannelLifetime != currentConfiguration.ChannelLifetime)
            {
                return false;
            }

            if (defaultConfiguration.MaxArrayLength != currentConfiguration.MaxArrayLength)
            {
                return false;
            }

            if (defaultConfiguration.MaxBufferSize != currentConfiguration.MaxBufferSize)
            {
                return false;
            }

            if (defaultConfiguration.MaxByteStringLength != currentConfiguration.MaxByteStringLength)
            {
                return false;
            }

            if (defaultConfiguration.MaxMessageSize != currentConfiguration.MaxMessageSize)
            {
                return false;
            }

            if (defaultConfiguration.MaxStringLength != currentConfiguration.MaxStringLength)
            {
                return false;
            }

            if (defaultConfiguration.OperationTimeout != currentConfiguration.OperationTimeout)
            {
                return false;
            }

            if (defaultConfiguration.SecurityTokenLifetime != currentConfiguration.SecurityTokenLifetime)
            {
                return false;
            }
            
            if (defaultConfiguration.UseBinaryEncoding != currentConfiguration.UseBinaryEncoding)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Finds the best match for the current protocol and security selections.
        /// </summary>
        private EndpointDescription FindBestEndpointDescription(EndpointDescriptionCollection endpoints)
        {            
            // filter by the current protocol.
            string currentProtocol = (string)ProtocolCB.SelectedItem;

            // filter by the current security mode.
            MessageSecurityMode currentMode = MessageSecurityMode.None;
            
            if (SecurityModeCB.SelectedIndex != -1)
            {
                currentMode = (MessageSecurityMode)SecurityModeCB.SelectedItem;
            }
            
            // filter by the current security policy.
            string currentPolicy = (string)SecurityPolicyCB.SelectedItem;

            // find all matching descriptions.      
            EndpointDescriptionCollection matches = new EndpointDescriptionCollection();

            if (endpoints != null)
            {
                foreach (EndpointDescription endpoint in endpoints)
                {
                    Uri url = Utils.ParseUri(endpoint.EndpointUrl);

                    if (url == null)
                    {
                        continue;
                    }

                    if (currentProtocol != url.Scheme)
                    {
                        continue;
                    }
                    
                    if (currentMode != endpoint.SecurityMode)
                    {
                        continue;
                    }
                    
                    if (currentPolicy != SecurityPolicies.GetDisplayName(endpoint.SecurityPolicyUri))
                    {
                        continue;
                    }

                    matches.Add(endpoint);
                }
            }
            
            // check for no matches.
            if (matches.Count == 0)
            {
                return null;
            }

            // check for single match.
            if (matches.Count == 1)
            {
                return matches[0];
            }

            // choose highest priority.
            EndpointDescription bestMatch = matches[0];

            for (int ii = 1; ii < matches.Count; ii++)
            {
                if (bestMatch.SecurityLevel < matches[ii].SecurityLevel)
                {
                    bestMatch = matches[ii];
                }
            }

            return bestMatch;
        }

        /// <summary>
        /// Finds the best match for the current protocol and security selections.
        /// </summary>
        private int FindBestUserTokenPolicy(EndpointDescription endpoint)
        {            
            // filter by the current token type.
            UserTokenType currentTokenType = UserTokenType.Anonymous;
            
            if (UserTokenTypeCB.SelectedIndex != -1)
            {
                currentTokenType = (UserTokenType)UserTokenTypeCB.SelectedItem;
            }
            
            // filter by issued token type.
            string currentIssuedTokenType = (string)IssuedTokenTypeCB.SelectedItem;

            // find all matching descriptions.      
            UserTokenPolicyCollection matches = new UserTokenPolicyCollection();

            if (endpoint != null)
            {
                for (int ii = 0; ii < endpoint.UserIdentityTokens.Count; ii++)
                {
                    UserTokenPolicy policy = endpoint.UserIdentityTokens[ii];

                    if (currentTokenType != policy.TokenType)
                    {
                        continue;
                    }

                    if (policy.TokenType == UserTokenType.IssuedToken)
                    {
                        if (currentIssuedTokenType != policy.IssuedTokenType)
                        {
                            continue;
                        }
                    }
                    
                    return ii;
                }
            }

            return -1;
        }

        /// <summary>
        /// Initializes the protocol dropdown.
        /// </summary>
        private void InitializeProtocols(EndpointDescriptionCollection endpoints)
        {
            // preserve the existing value.
            string currentProtocol = (string)ProtocolCB.SelectedItem;
 
            ProtocolCB.Items.Clear();

            // set all available protocols.
            if (m_showAllOptions)
            {
                ProtocolCB.Items.Add(Utils.UriSchemeHttp);
                ProtocolCB.Items.Add(Utils.UriSchemeOpcTcp);
            }

            // find all unique protocols.
            else
            {
                if (endpoints != null)
                {
                    foreach (EndpointDescription endpoint in endpoints)
                    {
                        Uri url = Utils.ParseUri(endpoint.EndpointUrl);

                        if (url != null)
                        {
                            int existingIndex = ProtocolCB.FindStringExact(url.Scheme);

                            if (existingIndex == -1)
                            {
                                ProtocolCB.Items.Add(url.Scheme);
                            }
                        }
                    }
                }

                // add at least one protocol.
                if (ProtocolCB.Items.Count == 0)
                {
                    ProtocolCB.Items.Add(Utils.UriSchemeHttp);
                }
            }

            // set the current value.
            int index = 0;

            if (!String.IsNullOrEmpty(currentProtocol))
            {
                index = ProtocolCB.FindStringExact(currentProtocol);
 
                if (index == -1)
                {
                    index = 0;
                }
            }

            ProtocolCB.SelectedIndex = index;
        }
        
        /// <summary>
        /// Initializes the security modes dropdown.
        /// </summary>
        private void InitializeSecurityModes(EndpointDescriptionCollection endpoints)
        {
            // filter by the current protocol.
            string currentProtocol = (string)ProtocolCB.SelectedItem;
            
            // preserve the existing value.
            MessageSecurityMode currentMode = MessageSecurityMode.None;
            
            if (SecurityModeCB.SelectedIndex != -1)
            {
                currentMode = (MessageSecurityMode)SecurityModeCB.SelectedItem;
            }

            SecurityModeCB.Items.Clear();

            // set all available security modes.
            if (m_showAllOptions)
            {
                SecurityModeCB.Items.Add(MessageSecurityMode.None);
                SecurityModeCB.Items.Add(MessageSecurityMode.Sign);
                SecurityModeCB.Items.Add(MessageSecurityMode.SignAndEncrypt);
            }

            // find all unique security modes.
            else
            {
                if (endpoints != null)
                {
                    foreach (EndpointDescription endpoint in endpoints)
                    {
                        Uri url = Utils.ParseUri(endpoint.EndpointUrl);

                        if (url != null)
                        {
                            if (currentProtocol != url.Scheme)
                            {
                                continue;
                            }

                            if (!SecurityModeCB.Items.Contains(endpoint.SecurityMode))
                            {
                                SecurityModeCB.Items.Add(endpoint.SecurityMode);
                            }
                        }
                    }
                }

                // add at least one policy.
                if (SecurityModeCB.Items.Count == 0)
                {
                    SecurityModeCB.Items.Add(MessageSecurityMode.None);
                }
            }

            // set the current value.
            int index = SecurityModeCB.Items.IndexOf(currentMode);

            if (index == -1)
            {
                index = 0;
            }

            SecurityModeCB.SelectedIndex = index;
        }
        
        /// <summary>
        /// Initializes the security policies dropdown.
        /// </summary>
        private void InitializeSecurityPolicies(EndpointDescriptionCollection endpoints)
        {
            // filter by the current protocol.
            string currentProtocol = (string)ProtocolCB.SelectedItem;

            // filter by the current security mode.
            MessageSecurityMode currentMode = MessageSecurityMode.None;
            
            if (SecurityModeCB.SelectedIndex != -1)
            {
                currentMode = (MessageSecurityMode)SecurityModeCB.SelectedItem;
            }

            // preserve the existing value.
            string currentPolicy = (string)SecurityPolicyCB.SelectedItem;

            SecurityPolicyCB.Items.Clear();
            
            // set all available security policies.
            if (m_showAllOptions)
            {
                SecurityPolicyCB.Items.Add(SecurityPolicies.GetDisplayName(SecurityPolicies.None));
                SecurityPolicyCB.Items.Add(SecurityPolicies.GetDisplayName(SecurityPolicies.Basic128Rsa15));
                SecurityPolicyCB.Items.Add(SecurityPolicies.GetDisplayName(SecurityPolicies.Basic256));
            }

            // find all unique security policies.    
            else
            {        
                if (endpoints != null)
                {
                    foreach (EndpointDescription endpoint in endpoints)
                    {
                        Uri url = Utils.ParseUri(endpoint.EndpointUrl);

                        if (url != null)
                        {
                            if (currentProtocol != url.Scheme)
                            {
                                continue;
                            }

                            if (currentMode != endpoint.SecurityMode)
                            {
                                continue;
                            }

                            string policyName = SecurityPolicies.GetDisplayName(endpoint.SecurityPolicyUri);

                            int existingIndex = SecurityPolicyCB.FindStringExact(policyName);

                            if (existingIndex == -1)
                            {
                                SecurityPolicyCB.Items.Add(policyName);
                            }
                        }
                    }
                }
            }

            // add at least one policy.
            if (SecurityPolicyCB.Items.Count == 0)
            {
                SecurityPolicyCB.Items.Add(SecurityPolicies.GetDisplayName(SecurityPolicies.None));
            }

            // set the current value.
            int index = 0;

            if (!String.IsNullOrEmpty(currentPolicy))
            {
                index = SecurityPolicyCB.FindStringExact(currentPolicy);
 
                if (index == -1)
                {
                    index = 0;
                }
            }

            SecurityPolicyCB.SelectedIndex = index;
        }

        /// <summary>
        /// Initializes the message encodings dropdown.
        /// </summary>
        private void InitializeEncodings(EndpointDescription endpoint)
        {
            // preserve the existing value.
            Encoding currentEncoding = Encoding.Default;
            
            if (EncodingCB.SelectedIndex != -1)
            {
                currentEncoding = (Encoding)EncodingCB.SelectedItem;
            }

            EncodingCB.Items.Clear();

            if (endpoint != null)
            {
                switch (endpoint.EncodingSupport)
                {
                    case BinaryEncodingSupport.None:
                    {
                        EncodingCB.Items.Add(Encoding.Xml);
                        break;
                    }

                    case BinaryEncodingSupport.Required:
                    {
                        EncodingCB.Items.Add(Encoding.Binary);
                        break;
                    }

                    case BinaryEncodingSupport.Optional:
                    {
                        EncodingCB.Items.Add(Encoding.Binary);
                        EncodingCB.Items.Add(Encoding.Xml);
                        break;
                    }
                }
            }

            // add at least one encoding.
            if (EncodingCB.Items.Count == 0)
            {
                EncodingCB.Items.Add(Encoding.Default);
            }

            // set the current value.
            int index = EncodingCB.Items.IndexOf(currentEncoding);

            if (index == -1)
            {
                index = 0;
            }

            EncodingCB.SelectedIndex = index;
        }
        
        /// <summary>
        /// Initializes the user token types dropdown.
        /// </summary>
        private void InitializeUserTokenTypes(EndpointDescription endpoint)
        {
            // preserve the existing value.
            UserTokenType currentTokenType = UserTokenType.Anonymous;
            
            if (UserTokenTypeCB.SelectedIndex != -1)
            {
                currentTokenType = (UserTokenType)UserTokenTypeCB.SelectedItem;
            }

            UserTokenTypeCB.Items.Clear();

            // show all options.
            if (m_showAllOptions)
            {
                UserTokenTypeCB.Items.Add(UserTokenType.Anonymous);
                UserTokenTypeCB.Items.Add(UserTokenType.UserName);
                UserTokenTypeCB.Items.Add(UserTokenType.Certificate);
                UserTokenTypeCB.Items.Add(UserTokenType.IssuedToken);
            }

            // find all unique token types.  
            else
            {
                if (endpoint != null)
                {
                    foreach (UserTokenPolicy policy in endpoint.UserIdentityTokens)
                    {
                        if (!UserTokenTypeCB.Items.Contains(policy.TokenType))
                        {
                            UserTokenTypeCB.Items.Add(policy.TokenType);
                        }
                    }
                }

                // add at least one policy.
                if (UserTokenTypeCB.Items.Count == 0)
                {
                    UserTokenTypeCB.Items.Add(UserTokenType.Anonymous);
                }
            }

            // set the current value.
            int index = UserTokenTypeCB.Items.IndexOf(currentTokenType);

            if (index == -1)
            {
                index = 0;
            }

            UserTokenTypeCB.SelectedIndex = index;
        }
        
        /// <summary>
        /// Initializes the user identity control.
        /// </summary>
        private void InitializeIssuedTokenType(EndpointDescription endpoint)
        {
            // get the current user token type.
            UserTokenType currentTokenType = UserTokenType.Anonymous;
            
            if (UserTokenTypeCB.SelectedIndex != -1)
            {
                currentTokenType = (UserTokenType)UserTokenTypeCB.SelectedItem;
            }
            
            // preserve the existing value.
            string currentIssuedTokenType = (string)IssuedTokenTypeCB.SelectedItem;

            IssuedTokenTypeCB.Items.Clear();

            // only applies to issued tokens.
            if (currentTokenType != UserTokenType.IssuedToken)
            {
                IssuedTokenTypeCB.Enabled = false;
                return;
            }

            // find all unique issued token types.            
            if (endpoint != null)
            {
                foreach (UserTokenPolicy policy in endpoint.UserIdentityTokens)
                {
                    if (!String.IsNullOrEmpty(policy.IssuedTokenType))
                    {
                        if (!IssuedTokenTypeCB.Items.Contains(policy.IssuedTokenType))
                        {
                            IssuedTokenTypeCB.Items.Add(policy.TokenType);
                        }
                    }
                }
            }

            // add at least one token type.
            if (IssuedTokenTypeCB.Items.Count == 0)
            {
                IssuedTokenTypeCB.Items.Add(String.Empty);
            }

            // set the current value.
            int index = IssuedTokenTypeCB.Items.IndexOf(currentIssuedTokenType);

            if (index == -1)
            {
                index = 0;
            }

            IssuedTokenTypeCB.SelectedIndex = index;
            IssuedTokenTypeCB.Enabled =  true;
        }

        /// <summary>
        /// Initializes the user identity control.
        /// </summary>
        private void InitializeUserIdentity(ConfiguredEndpoint endpoint)
        {
            // get the current user token type.
            UserTokenType currentTokenType = UserTokenType.Anonymous;
            
            if (UserTokenTypeCB.SelectedIndex != -1)
            {
                currentTokenType = (UserTokenType)UserTokenTypeCB.SelectedItem;
            }
            
            // set the default values.
            UserIdentityTB.Text = null;
            UserIdentityTB.Enabled = currentTokenType != UserTokenType.Anonymous;
            UserIdentityBTN.Enabled = currentTokenType != UserTokenType.Anonymous;
            
            // update from endpoint.
            if (endpoint != null && endpoint.UserIdentity != null)
            {
                UserIdentityTB.Text = endpoint.UserIdentity.ToString();
            }
        }

        /// <summary>
        /// Attempts fetch the list of servers from the discovery server.
        /// </summary>
        private void OnDiscoverEndpoints(object state)
        {
            int discoverCount = m_discoverCount;

            // do nothing if a valid list is not provided.
            ApplicationDescription server = state as ApplicationDescription;

            if (server == null)
            {
                return;

            }

            OnUpdateStatus("Attempting to read latest configuration options from server.");

            // process each url.
            foreach (string discoveryUrl in server.DiscoveryUrls)
            {
                Uri url = Utils.ParseUri(discoveryUrl);

                if (url != null)
                {
                    if (DiscoverEndpoints(url))
                    {
                        m_discoverySucceeded = true;
                        m_discoveryUrl = url;
                        OnUpdateStatus("Configuration options are up to date.");
                        return;
                    }

                    // check if another discover operation has started.
                    if (discoverCount != m_discoverCount)
                    {
                        return;
                    }
                }
            }

            OnUpdateEndpoints(null);
            OnUpdateStatus("Configuration options may not be correct because the server is not available.");
        }

        /// <summary>
        /// Fetches the servers from the discovery server.
        /// </summary>
        private bool DiscoverEndpoints(Uri discoveryUrl)
        {
            // use a short timeout.
            EndpointConfiguration configuration = EndpointConfiguration.Create(m_configuration);
            configuration.OperationTimeout = m_discoveryTimeout;

            DiscoveryClient client = DiscoveryClient.Create(
                discoveryUrl,
                BindingFactory.Create(m_configuration, m_configuration.CreateMessageContext()), 
                EndpointConfiguration.Create(m_configuration));

            try
            {
                EndpointDescriptionCollection endpoints = client.GetEndpoints(null);
                OnUpdateEndpoints(endpoints);
                return true;
            }
            catch (Exception e)
            {
                Utils.Trace("Could not fetch endpoints from url: {0}. Reason={1}", discoveryUrl, e.Message);
                return false;
            }
            finally
            {
                client.Close();
            }
        }
        
        /// <summary>
        /// Updates the status displayed in the dialog.
        /// </summary>
        private void OnUpdateStatus(object status)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new WaitCallback(OnUpdateStatus), status);
                return;
            }

            StatusTB.Text = status as string;
        }
        
        /// <summary>
        /// Updates the list of servers displayed in the control.
        /// </summary>
        private void OnUpdateEndpoints(object state)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new WaitCallback(OnUpdateEndpoints), state);
                return;
            }

            // get the updated descriptions.
            EndpointDescriptionCollection endpoints = state as EndpointDescriptionCollection;

            if (endpoints == null)
            {
                m_showAllOptions = true;
                InitializeProtocols(m_availableEndpoints);
            }

            else
            {
                m_showAllOptions = false;
                m_availableEndpoints = endpoints;

                if (endpoints.Count > 0)
                {
                    m_currentDescription = endpoints[0];
                }

                // initializing the protocol will trigger an update to all other controls.
                InitializeProtocols(m_availableEndpoints);

                // select the best security mode.
                MessageSecurityMode bestMode = MessageSecurityMode.Invalid;

                foreach (MessageSecurityMode securityMode in SecurityModeCB.Items)
                {
                    if (securityMode > bestMode)
                    {
                        bestMode = securityMode;
                    }
                }
                        
                SecurityModeCB.SelectedItem = bestMode;

                // select the best encoding.
                Encoding bestEncoding = Encoding.Default;

                foreach (Encoding encoding in EncodingCB.Items)
                {
                    if (encoding > bestEncoding)
                    {
                        bestEncoding = encoding;
                    }
                }
                        
                EncodingCB.SelectedItem = bestEncoding;
            }

            if (m_endpoint != null)
            {
                Uri url = m_endpoint.EndpointUrl;

                foreach (string protocol in ProtocolCB.Items)
                {
                    if (protocol == url.Scheme)
                    {
                        ProtocolCB.SelectedItem = url.Scheme;
                        break;
                    }
                }

                foreach (MessageSecurityMode securityMode in SecurityModeCB.Items)
                {
                    if (securityMode == m_endpoint.Description.SecurityMode)
                    {
                        SecurityModeCB.SelectedItem = securityMode;
                        break;
                    }
                }

                foreach (string securityPolicy in SecurityPolicyCB.Items)
                {
                    if (securityPolicy == m_endpoint.Description.SecurityPolicyUri)
                    {
                        SecurityPolicyCB.SelectedItem = securityPolicy;
                        break;
                    }
                }

                foreach (Encoding encoding in EncodingCB.Items)
                {
                    if (encoding == Encoding.Binary && m_endpoint.Configuration.UseBinaryEncoding)
                    {
                        EncodingCB.SelectedItem = encoding;
                        break;
                    }

                    if (encoding == Encoding.Xml && !m_endpoint.Configuration.UseBinaryEncoding)
                    {
                        EncodingCB.SelectedItem = encoding;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Creates the endpoint description from current selections.
        /// </summary>
        private EndpointDescription CreateDescriptionFromSelections()
        {
            string currentProtocol = (string)ProtocolCB.SelectedItem;

            EndpointDescription endpoint = null;

            for (int ii = 0; ii < m_availableEndpoints.Count; ii++)
            {
                Uri url = Utils.ParseUri(m_availableEndpoints[ii].EndpointUrl);

                if (url == null)
                {
                    continue;
                }

                if (endpoint == null)
                {
                    endpoint = m_availableEndpoints[ii];
                }

                if (url.Scheme == currentProtocol)
                {
                    endpoint = m_availableEndpoints[ii];
                    break;
                }
            }

            UriBuilder builder = null;

            if (endpoint == null)
            {
                builder = new UriBuilder();
                builder.Host = "localhost";

                if (currentProtocol == Utils.UriSchemeOpcTcp)
                {
                    builder.Port = Utils.UaTcpDefaultPort;
                }
            }
            else
            {
                builder = new UriBuilder(endpoint.EndpointUrl);
            }

            builder.Scheme = currentProtocol;

            endpoint = new EndpointDescription();
            endpoint.EndpointUrl = builder.ToString();
            endpoint.SecurityMode = (MessageSecurityMode)SecurityModeCB.SelectedItem;
            endpoint.SecurityPolicyUri = SecurityPolicies.GetUri((string)SecurityPolicyCB.SelectedItem);
            endpoint.Server.ApplicationName = endpoint.EndpointUrl;
            endpoint.Server.ApplicationType = ApplicationType.Server;
            endpoint.Server.ApplicationUri = endpoint.EndpointUrl;

            UserTokenType userTokenType = (UserTokenType)UserTokenTypeCB.SelectedItem;
            UserTokenPolicy userTokenPolicy = new UserTokenPolicy(userTokenType);

            if (userTokenType == UserTokenType.IssuedToken)
            {
                userTokenPolicy.IssuedTokenType = (string)IssuedTokenTypeCB.SelectedItem;
            }

            endpoint.UserIdentityTokens.Add(userTokenPolicy);

            return endpoint;
        }
        #endregion

        #region Event Handlers
        private void OkBTN_Click(object sender, EventArgs e)
        {
            try
            {
                // check that discover has completed.
                if (!m_discoverySucceeded)
                {
                    DialogResult result = MessageBox.Show(
                        "Endpoint information may be out of date because the discovery process has not completed. Continue anyways?",
                        this.Text,
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result != DialogResult.Yes)
                    {
                        return;
                    }
                }

                EndpointConfiguration configuration = m_endpointConfiguration;

                if (configuration == null)
                {
                    configuration = EndpointConfiguration.Create(m_configuration);
                }

                if (m_currentDescription == null)
                {
                    m_currentDescription = CreateDescriptionFromSelections();
                }
                
                // the discovery endpoint should always be on the same machine as the server.
                // if there is a mismatch it is likely because the server has multiple addresses
                // and was not configured to return the current address to the client.
                // The code automatically updates the domain in the url. 
                Uri endpointUrl = Utils.ParseUri(m_currentDescription.EndpointUrl);

                if (m_discoverySucceeded)
                {
                    if (!Utils.AreDomainsEqual(endpointUrl, m_discoveryUrl))
                    {
                        UriBuilder url = new UriBuilder(endpointUrl);

                        url.Host = m_discoveryUrl.DnsSafeHost;

                        if (url.Scheme == m_discoveryUrl.Scheme)
                        {
                            url.Port = m_discoveryUrl.Port;
                        }

                        endpointUrl = url.Uri;

                        m_currentDescription.EndpointUrl = endpointUrl.ToString();
                    }
                }

                // set the encoding.
                Encoding encoding = (Encoding)EncodingCB.SelectedItem;
                configuration.UseBinaryEncoding = encoding != Encoding.Xml;

                if (m_endpoint == null)
                {
                    m_endpoint = new ConfiguredEndpoint(null, m_currentDescription, configuration);
                }
                else
                {
                    m_endpoint.Update(m_currentDescription);
                    m_endpoint.Update(configuration);
                }
                
                // set the user token policy.
                m_endpoint.SelectedUserTokenPolicyIndex = FindBestUserTokenPolicy(m_currentDescription);

                // update the user identity.
                UserTokenType userTokenType = (UserTokenType)UserTokenTypeCB.SelectedItem;

                UserIdentityToken userIdentity = null;

                if (!m_userIdentities.TryGetValue(userTokenType, out userIdentity))
                {
                    userIdentity = null;
                }
                
                m_endpoint.UserIdentity = userIdentity;

                DialogResult = DialogResult.OK;
            }
            catch (Exception exception)
            {
                GuiUtils.HandleException(this.Text, MethodBase.GetCurrentMethod(), exception);
            }
        }

        private void ProtocolCB_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                m_updating = true;
                InitializeSecurityModes(m_availableEndpoints);

                // update current description.
                m_currentDescription = FindBestEndpointDescription(m_availableEndpoints);

                InitializeEncodings(m_currentDescription);
                InitializeUserTokenTypes(m_currentDescription);
            }
            catch (Exception exception)
            {
                GuiUtils.HandleException(this.Text, MethodBase.GetCurrentMethod(), exception);
            }
            finally
            {
                m_updating = false;
            }
        }

        private void SecurityModeCB_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                InitializeSecurityPolicies(m_availableEndpoints);

                if (!m_updating)
                {
                    try
                    {
                        m_updating = true;

                        // update current description.
                        m_currentDescription = FindBestEndpointDescription(m_availableEndpoints);

                        InitializeEncodings(m_currentDescription);
                        InitializeUserTokenTypes(m_currentDescription);
                    }
                    finally
                    {
                        m_updating = false;
                    }
                }
            }
            catch (Exception exception)
            {
                GuiUtils.HandleException(this.Text, MethodBase.GetCurrentMethod(), exception);
            }
        }

        private void SecurityPolicyCB_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (!m_updating)
                {
                    try
                    {
                        m_updating = true;

                        // update current description.
                        m_currentDescription = FindBestEndpointDescription(m_availableEndpoints);

                        InitializeEncodings(m_currentDescription);
                        InitializeUserTokenTypes(m_currentDescription);
                    }
                    finally
                    {
                        m_updating = false;
                    }
                }
            }
            catch (Exception exception)
            {
                GuiUtils.HandleException(this.Text, MethodBase.GetCurrentMethod(), exception);
            }
        }

        private void UserTokenPolicyCB_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                InitializeIssuedTokenType(m_currentDescription);
                InitializeUserIdentity(m_endpoint);
            }
            catch (Exception exception)
            {
                GuiUtils.HandleException(this.Text, MethodBase.GetCurrentMethod(), exception);
            }
        }

        private void OverrideLimitsCB_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                int index = UseDefaultLimitsCB.SelectedIndex;

                if (index != -1)
                {
                    UseDefaultLimitsBTN.Enabled = (UseDefaultLimits)UseDefaultLimitsCB.SelectedItem == UseDefaultLimits.No;
                }
            }
            catch (Exception exception)
            {
                GuiUtils.HandleException(this.Text, MethodBase.GetCurrentMethod(), exception);
            }
        }
        #endregion
    }
}
