# SwitchNet support

Playing a game against a self-hosted [SwitchNet](https://github.com/n-popescu/switchnet)
server instead of Nintendo's.

Everything here is off by default and does nothing until you turn it on.

## What it does, and what it does not

There are **two** halves, and they solve different problems. Both are needed.

| | What it covers | Where it is set |
| --- | --- | --- |
| **Traffic redirect** | the *game's own* connections — the game server, friends, accounts | the DNS-MITM hosts file on the emulated SD card |
| **Account login** | the *emulator* fetching an identity token to hand the game | Settings → Network |

The account login exists because a game asks the account service for a BAAS
identity token before it can reach any online game server. Ryujinx generates one
locally: the right shape, signed with an RSA key it makes up on the spot. That
is fine against a server that never checks the signature, and useless against
one that does — which SwitchNet does.

With a login configured, the emulator performs the same three calls a console
makes, against your server, and hands the game the token **your server signed**:

1. `POST dauth /v6/device_auth_token` — a device token.
2. `POST BAAS /1.0.0/application/token` — an anonymous access token.
3. `POST BAAS /1.0.0/login` — the device account id and password, returning the
   id token.

No Nintendo credential is presented, verified or forged anywhere in this. Every
token involved is one your own server signed with its own key.

## Setting it up

### 1. Make an account on the server

```sh
switchnetctl account create \
  -device-account-id 0123456789abcdef \
  -password 'something-long' \
  -nickname 'Me'
```

The device account id is 16 hex characters. The password is a SwitchNet
credential for your own private server — it is not a Nintendo password and
cannot be used as one.

### 2. Redirect the guest's traffic

Generate the hosts file on the server and put it on the emulated SD card:

```sh
switchnetctl hosts > sdcard/atmosphere/hosts/default.txt
```

Ryujinx's SD card lives under its data directory (`Ryujinx/sdcard/`, or
`portable/sdcard/` in portable mode). This is the same file and format a real
console uses, so an existing one works unmodified.

Then turn on **SwitchNet Support** in *Settings → Network*. That makes a
redirected host bypass the DNS blacklist — which normally makes those hostnames
fail to resolve at all — and trust the certificate your server presents, since it
will not be signed by a real certificate authority. Both are scoped to hosts the
hosts file actually redirects; nothing else the guest connects to is affected.

### 3. Fill in the login

Still in *Settings → Network*, under the SwitchNet Support checkbox:

- **SwitchNet Server** — where your server is, e.g. `192.168.1.50`, or
  `192.168.1.50:8443` if the edge is not on 443. A hostname works too.
- **Device Account ID** — from step 1.
- **Device Account Password** — from step 1.

Point this at the **same server** the hosts file does. They are separate
mechanisms and nothing forces them to agree; if they disagree, the game talks to
one server while the emulator logs into another, and the game server rejects a
token it never issued.

Leaving these empty is a valid configuration: the traffic redirect still works,
and the game gets the locally generated token as before.

## When it does not work

Failures are logged once per distinct reason under `ServiceAcc`, with the actual
cause rather than a generic failure:

| Log line | What to do |
| --- | --- |
| `invalid_device_account` | the device account id or password is wrong |
| `could not reach … at <address>` | the server address is wrong, or the server is not running |
| `is not a host or host:port` | the address field is malformed |
| `did not finish within 10s` | the server is reachable but not answering |

After any of these the game falls back to the locally generated token, so it
will still start and then fail at the game server. The log line is the diagnosis.

## Splatoon 3 needs one more thing

Splatoon 3 does not use the console's TLS stack. Its gRPC/HTTP2 game-server
client (NPLN) does TLS itself, over a raw socket, with its own embedded
BoringSSL and its own pinned certificates — so the emulator only ever sees
ciphertext and the certificate trust above cannot reach it.

That needs the same certificate-pinning patch a real console running SwitchNet
needs: `kinnay/NPLN-Protocols` ships `generate_patch.py`, which builds an IPS
patch for the game's own executable. Applied as an ordinary Ryujinx
`exefs_patches` mod. Two things worth knowing about it: it *disables*
verification rather than adding trust, and it is keyed to the game's build id,
so a title update silently stops it applying.
