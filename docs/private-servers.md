# Private Nintendo servers

Ryujinx can be pointed at a private replacement for Nintendo's online servers — a server you run,
for consoles and emulators you own — so that a game's account, friends and online features talk to
it instead of Nintendo.

Two settings, under *Settings → Network → Private Nintendo Servers*. **Both are needed**, and they
solve two different problems that are easy to confuse:

| | Problem | Setting |
| --- | --- | --- |
| **1** | Nintendo's hostnames have to resolve to your server | *Redirect Nintendo's hostnames using the hosts file on the SD card* |
| **2** | The guest has to accept your server's certificate for those hostnames | *Trusted CA certificate* |

Neither one turns certificate verification off. That distinction is the whole design and is worth
reading [section 3](#3-certificate-trust-adds-a-ca-it-does-not-disable-verification) for.

> Use this with servers you run and games you own. This adds nothing that helps against Nintendo's
> own servers: it only makes the emulator willing to talk to a server you have set up yourself.

---

## 1. Setting it up

**Enable guest Internet access** first, under *Settings → Network → Network Connection*. Without it
every lookup is refused before any of this is reached, and the redirect checkbox stays disabled.

**Put a hosts file on the virtual SD card**, at:

```
<Ryujinx data>/sdcard/atmosphere/hosts/default.txt
```

The format is Atmosphère's, which is an ordinary hosts file plus two extensions Ryujinx also
supports: `*` as a wildcard in a hostname, and `%` expanding to the environment (`lp1`).

```
192.168.1.50   dauth-lp1.ndas.srv.nintendo.net
192.168.1.50   *.baas.nintendo.com
192.168.1.50   accounts.nintendo.com
0.0.0.0        receive-%.dg.srv.nintendo.net
```

If your server generates this file, use the generated one — the hostnames a private server serves
are its business, not something to transcribe by hand.

**Turn on the redirect checkbox.** Ryujinx already read this file before this feature existed; what
the checkbox changes is that a hostname on the built-in DNS block list is now allowed to be
redirected. See [section 2](#2-the-block-list-and-why-the-override-is-narrow).

**Point the CA setting at your server's certificate authority**, as a PEM file. This is the file your
server's certificate-generation step produced — often called `rootCA.pem` or `ca.crt`. It is the
public certificate, not the private key.

**Restart the game.** Both settings apply to new lookups and new connections; a game that has
already connected keeps what it has.

---

## 2. The block list, and why the override is narrow

Ryujinx blocks some of Nintendo's hostnames outright, in
`Ryujinx.Common/Helpers/Patterns.cs` → `BlockedHosts`. That block list is checked **before** the
hosts file, so until now a blocked hostname was refused even if you had pointed it at your own
machine.

That is the right default and exactly wrong for this one case, because a private server lives at
those hostnames — that is what the guest asks for. Among the blocked patterns:

| Pattern | Includes |
| --- | --- |
| `*-lp1.lp1.t.npln.srv.nintendo.net` | NPLN game servers, e.g. Splatoon 3's tenant |
| `accounts.nintendo.com` | the Nintendo Account server |
| `*-lp1.n.n.srv.nintendo.net` | the NAT-check pair |
| `*-lp1.znc.srv.nintendo.net` | the mobile-app service |

**The override applies only to a hostname the hosts file actually names.** A blocked hostname with
no entry stays blocked. That is deliberate and it is the property to preserve if this code is ever
changed: turning the setting on is a *redirect to a server you specified*, not a general unblocking,
so it cannot open a path to Nintendo's own servers for anything you have not explicitly redirected.

`DnsBlacklistTests` covers it, including the case that matters — a blocked host with no entry, with
the override on, still refused.

---

## 3. Certificate trust adds a CA; it does not disable verification

A server answering `dauth-lp1.ndas.srv.nintendo.net` cannot have a certificate any public authority
would issue for that name, so it signs its own with a CA you control. The guest's TLS validates
against your operating system's trust store and rejects it.

The obvious fix — a validation callback that returns `true` — is what most guides suggest and is much
worse than it looks. It would apply to **every** TLS connection the guest makes, including ones to
real Nintendo, for as long as the option is on.

So instead, the configured CA is *added*, in
`Ryujinx.HLE/HOS/Services/Ssl/SslService/PrivateServerTrust.cs`. Concretely:

| Situation | Result |
| --- | --- |
| The certificate already validates normally | accepted, without consulting your CA |
| Signed by your CA, and covers the hostname asked for | **accepted** |
| Signed by your CA, wrong hostname | **rejected** |
| Signed by your CA, expired | **rejected** |
| Self-signed, or signed by some other CA | **rejected** |
| No certificate at all | rejected |

The hostname check surviving is the important row. It means that redirecting a hostname to a server
whose certificate does not cover it fails *here*, loudly, instead of silently succeeding — which is
what keeps this a redirect to a known server rather than a hole. `PrivateServerTrustTests` asserts
each row.

A bundle may hold several PEM blocks, for two servers or a CA rotation, and an intermediate the
server presents on the wire is used to build the chain, so a server that keeps its root offline
works with only the root configured.

---

## 4. Troubleshooting

| Symptom | Almost certainly |
| --- | --- |
| `DNS Blocked: <host>` in the log | The hosts file has no entry for it. The override only covers hostnames the file names. |
| `Trying to resolve: <host>` and then a connection failure | The hostname resolved but the server did not answer. Not an emulator problem: check the server and the port. |
| No log line at all for a hostname | Guest Internet access is off, or the game never asked for it. |
| `Private server CA bundle not found` | The path is wrong. It is read at handshake time, not when you set it. |
| `The server certificate does not chain to a trusted private CA` | The file is not the CA that signed the server's certificate, or the server is not presenting its intermediate. |
| `Rejecting the server certificate: RemoteCertificateNameMismatch` | The server's certificate does not cover the hostname the game asked for. This is a real misconfiguration and is not forgiven — see section 3. |
| The certificate settings changed and nothing happened | Both apply to new connections. Restart the game. Changing the *contents* of the CA file without changing its path needs a restart of the emulator, because the bundle is cached on its path. |

Turn on `Logging → Enable info logs` and watch for `ServiceSfdnsres` and `ServiceSsl`.

---

## 5. What this does not do

**It does not emulate a console's certificate store.** A real console has a private Nintendo CA and
needs its own work to trust a private server; that is a console-side problem and has nothing to do
with the emulator.

**It does not make a game work whose server does not exist.** Redirecting a hostname to a machine
running nothing gives a connection failure, which is a different and more honest failure than the
block list's.

**It does not carry local wireless.** That is [LAN Play](lan-play.md), and the two are independent —
a private server handles accounts and online play, LAN Play handles local multiplayer between
players.

---

## 6. Where the code is

| Concern | File |
| --- | --- |
| The block-list override | `Ryujinx.HLE/HOS/Services/Sockets/Sfdnsres/Proxy/DnsBlacklist.cs` |
| The hosts file | `Ryujinx.HLE/HOS/Services/Sockets/Sfdnsres/Proxy/DnsMitmResolver.cs` |
| Where both are used | `Ryujinx.HLE/HOS/Services/Sockets/Sfdnsres/IResolver.cs` |
| Certificate trust | `Ryujinx.HLE/HOS/Services/Ssl/SslService/PrivateServerTrust.cs` |
| Where it is applied | `Ryujinx.HLE/HOS/Services/Ssl/SslService/SslManagedSocketConnection.cs` |
| Settings | `Ryujinx/UI/Views/Settings/SettingsNetworkView.axaml` |
| Tests | `Ryujinx.Tests/HLE/{DnsBlacklistTests,PrivateServerTrustTests}.cs` |

Headless has both as `--redirect-nintendo-servers` and `--private-server-ca <path>`.
