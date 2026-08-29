# Private Nintendo servers

Ryujinx can be pointed at a private replacement for Nintendo's online servers — a server you run,
for consoles and emulators you own — so that a game's account, friends and online features talk to
it instead of Nintendo.

Settings under *Settings → Network → Private Nintendo Servers* solve two different problems that
are easy to confuse:

| | Problem | Setting |
| --- | --- | --- |
| **1** | Nintendo's hostnames have to resolve to your server | *either* the hosts file *or* a redirect address — see [section 1](#1-setting-it-up) |
| **2** | The guest has to accept your server's certificate for those hostnames | *Trusted CA certificate* |

**Problem 1 has two independent solutions**, and you can use either, or both together:

- **The hosts file on the virtual SD card.** Name a hostname, get an address. Precise, and lets
  different hostnames go to different addresses — the shape a real Atmosphère install already uses,
  so a private server that generates this file for a real console can hand you the identical one.
- **A single redirect address.** No hosts file at all: point every Nintendo hostname SwitchNet-style
  redirection covers at one address, in a text box. Simpler, and enough if your whole deployment is
  one server.

A hosts-file entry for a given hostname wins when both are configured — it is the more specific of
the two. Neither mechanism touches certificate trust, which is entirely separate: turning either one
on gets the guest talking to your server, but its TLS still rejects your server's certificate until
the CA setting below is configured too.

Certificate trust itself never turns verification off. That distinction is the whole design of
[section 3](#3-certificate-trust-adds-a-ca-it-does-not-disable-verification).

> Use this with servers you run and games you own. This adds nothing that helps against Nintendo's
> own servers: it only makes the emulator willing to talk to a server you have set up yourself.

---

## 1. Setting it up

**Enable guest Internet access** first, under *Settings → Network → Network Connection*. Without it
every lookup is refused before any of this is reached, and the hosts-file redirect checkbox stays
disabled.

### 1.1 The hosts file (precise, per hostname)

Put a hosts file on the virtual SD card, at:

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

### 1.2 One redirect address (simpler, whole deployment)

Skip the hosts file and type an address directly into **Redirect address**, under the checkbox.
Every Nintendo-online-service hostname the block list would otherwise refuse now resolves there
instead — without maintaining a file, and without the checkbox above needing to be on at all: **this
field is its own switch**, empty meaning no redirect, the same way the SwitchNet account fields
further down the page have no separate on/off next to them.

It has to be an IP address, not a hostname — the resolver this configures is precisely the one that
would have to resolve that hostname, and asking it to resolve one to redirect through would be
circular.

**One host needs its own address: `nncs2`.** Nintendo's NAT-check protocol probes `nncs1` and
`nncs2` as two separate servers, and one message type must be answered from a genuinely different
address or the console de-duplicates the probe and matchmaking finds matches that will not start.
Leaving **NAT-check secondary address** empty while **Redirect address** is set answers `nncs2` from
the primary address too, which has exactly that failure mode — so fill it in with a second address
if you are redirecting a title that uses Pia (Splatoon 3 does not; it uses ICE instead and never
touches `nncs1`/`nncs2`).

Both fields take effect on the next name the guest resolves — no restart needed, unlike the CA
setting below.

### 1.3 Either way: certificate trust

**Point the CA setting at your server's certificate authority**, as a PEM file. This is the file your
server's certificate-generation step produced — often called `rootCA.pem` or `ca.crt`. It is the
public certificate, not the private key. Needed regardless of which redirect mechanism above you
used — trust and redirection are independent, see [section 3](#3-certificate-trust-adds-a-ca-it-does-not-disable-verification).

**Restart the game** if you changed the CA setting. The redirect settings apply live; the CA bundle
is cached on its path and only re-reads on a restart of the emulator.

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

**Both mechanisms apply only to a hostname they actually name.** The hosts-file override applies
only to a hostname the file names; the redirect address applies only to a hostname the block list
already matches (Splatoon 3's NPLN tenant, the account server, the NAT-check pair, and so on — never
an arbitrary hostname). A blocked hostname neither one names stays blocked. That is deliberate and
it is the property to preserve if this code is ever changed: turning either setting on is a
*redirect to a server you specified*, not a general unblocking, so it cannot open a path to
Nintendo's own servers for anything you have not explicitly redirected.

`DnsBlacklistTests` covers it, including the case that matters for each mechanism — a blocked host
with no hosts-file entry and no redirect address configured, still refused.

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
| `DNS Blocked: <host>` in the log | Neither mechanism names this host: no hosts-file entry, and (if configured) the redirect address is empty or not a bare IP. |
| A game that redirects fine everywhere else never matches its opponents | Redirected `nncs1` and `nncs2` to the same address — check the NAT-check secondary address is set and genuinely different, or that your hosts file has separate entries for both. Splatoon 3 does not use `nncs1`/`nncs2` at all, so this only affects Pia titles. |
| `Trying to resolve: <host>` and then a connection failure | The hostname resolved but the server did not answer. Not an emulator problem: check the server and the port. |
| No log line at all for a hostname | Guest Internet access is off, or the game never asked for it. |
| `Private server CA bundle not found` | The path is wrong. It is read at handshake time, not when you set it. |
| `The server certificate does not chain to a trusted private CA` | The file is not the CA that signed the server's certificate, or the server is not presenting its intermediate. |
| `Rejecting the server certificate: RemoteCertificateNameMismatch` | The server's certificate does not cover the hostname the game asked for. This is a real misconfiguration and is not forgiven — see section 3. |
| The redirect address changed and nothing happened | It should have — redirect settings apply to the next lookup, no restart needed. If it truly did not, confirm the address is a bare IP literal (a hostname is silently ignored, see 1.2). |
| The certificate settings changed and nothing happened | The CA bundle is cached on its path and only re-reads on a restart of the emulator, unlike the redirect settings. |

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

Headless: `--redirect-nintendo-servers`, `--private-server-ca <path>`,
`--private-server-address <ip>` and `--private-server-natcheck-secondary-address <ip>`.
