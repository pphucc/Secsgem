namespace Secsgem.Core.Transport;

/// <summary>
/// HSMS connection mode (SEMI E37). Determines which party initiates the TCP connection.
/// Either host or equipment can be configured as Active or Passive — the mode is
/// independent of the application role.
/// </summary>
public enum HsmsConnectionMode
{
    /// <summary>This entity initiates the TCP connection to the remote party.</summary>
    Active,

    /// <summary>This entity listens and accepts TCP connections from the remote party.</summary>
    Passive
}
