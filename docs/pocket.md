# ClinicLive Pocket — the patient companion app (season three)

`src/ClinicLive.Pocket` is a .NET MAUI Blazor Hybrid app (Android + Windows here;
iOS/Mac Catalyst compile but were not built in the series — no Mac on the bench).
`src/ClinicLive.Pocket.Web` hosts the **same** Razor components as a Blazor web app.
`src/ClinicLive.Pocket.Shared` is where every screen lives, once.
`src/ClinicLive.Contracts` is the wire shape the phone and the server agree on.

The companion series: [From Prompt to Pocket](https://www.coder000.com/series/from-prompt-to-pocket).

## Run it against the clinic

```bash
docker compose up -d                       # PostgreSQL 18 on localhost:5499
cd src/ClinicLive && dotnet run            # the clinic on http://localhost:5159 (Development seeds DEMO00..DEMO44)
```

Then one of:

| Host | Command | Talks to the clinic at |
|---|---|---|
| Android emulator | `dotnet build src/ClinicLive.Pocket -f net10.0-android -t:Install` | `http://10.0.2.2:5159` (the emulator's name for your PC) |
| Android phone (USB/Wi-Fi) | same, then set **Settings › Clinic server** to `http://<your-PC-LAN-IP>:5159/` and restart | your PC — run the clinic with `--urls http://0.0.0.0:5159` and open the firewall |
| Windows | `dotnet build src/ClinicLive.Pocket -f net10.0-windows10.0.19041.0` then run the exe in `bin/` | `http://localhost:5159` |
| Browser | `cd src/ClinicLive.Pocket.Web && dotnet run` | `ClinicLive:ApiBase` in appsettings.json |

Plain `http` is allowed for `10.0.2.2` and `localhost` only
(`Platforms/Android/Resources/xml/network_security_config.xml`). A real deployment talks
`https` to a real hostname and needs none of that.

## Secrets and where they live

Nothing in this repository is a credential. Two files are git-ignored on purpose:

| File | Purpose | Where it goes |
|---|---|---|
| `google-services.json` | Firebase config for the Android package `com.cliniclive.pocket` (needed for push, Part 6) | `src/ClinicLive.Pocket/Platforms/Android/` — the build picks it up when present and simply skips Firebase resources when it isn't |
| Firebase service-account JSON | lets the **server** send push notifications | anywhere outside the repo; point at it with `dotnet user-secrets set "Push:ServiceAccountPath" "<full path>" --project src/ClinicLive` (server config in production). Unset → `NullPushSender` logs instead of sending. |

To get both: create a Firebase project (free Spark plan, Analytics off), add an Android
app with the package name above, download `google-services.json`; then Project settings
› Service accounts › *Generate new private key*.

## Ship it (Part 11)

### Android — a signed release build

Create a keystore **once**, outside the repo (this one is a demo identity, not the
series author's):

```bash
keytool -genkeypair -v -keystore .secrets/pocket-release.keystore -alias pocket \
        -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=ClinicLive Demo, O=ClinicLive, C=ZZ"
```

Then publish, passing the signing details as MSBuild properties (never in the csproj):

```bash
dotnet publish src/ClinicLive.Pocket -f net10.0-android -c Release \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore=$PWD/.secrets/pocket-release.keystore \
  -p:AndroidSigningKeyAlias=pocket \
  -p:AndroidSigningKeyPass=env:POCKET_KEY_PASS \
  -p:AndroidSigningStorePass=env:POCKET_STORE_PASS
```

Output: `bin/Release/net10.0-android/publish/com.cliniclive.pocket-Signed.apk` (sideload)
and `…-Signed.aab` (Play Store). Verify with `apksigner verify --print-certs <apk>`.

### Windows — self-contained, unpackaged

```bash
dotnet publish src/ClinicLive.Pocket -f net10.0-windows10.0.19041.0 -c Release \
  -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -p:SelfContained=true
```

A folder you can zip and hand to a receptionist: no .NET, no Windows App SDK to install.
Don't add `-r win-x64`: on a multi-target MAUI project that makes NuGet restore *every*
target framework for that runtime, and there is no Mono runtime pack for Android on
win-x64 (`NU1102 Microsoft.NETCore.App.Runtime.Mono.win-x64`). The Windows target already
defaults to win-x64.

**MSIX** (`-p:WindowsPackageType=MSIX -p:GenerateAppxPackageOnBuild=true`) gives the app
a package identity — which is also what makes Windows toast notifications work reliably
(Part 5's open item). Sideloading an MSIX needs a certificate the machine trusts, so the
series builds the package and stops there.

### CI

`.github/workflows/ci.yml` builds and tests the clinic (Ubuntu, real PostgreSQL via
Testcontainers), builds the Android app (Ubuntu: Android workload + Java 17, no
Firebase file → no push resources, still compiles) and the Windows app (windows-latest).
