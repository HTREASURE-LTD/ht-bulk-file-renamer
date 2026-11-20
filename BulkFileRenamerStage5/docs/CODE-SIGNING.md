# Code signing – HT Bulk File Renamer

Unsigned installers make Windows SmartScreen show **"Unknown publisher"**. Signing needs a code-signing certificate
issued to **HTREASURE LTD** after the issuer has verified the company; only HTREASURE LTD can apply for one. Until
then, releases can be **self-signed** (see below), which proves the files are HTREASURE's and unmodified but still shows
"Unknown publisher" on PCs that don't trust the certificate.

`Build-Release.ps1` supports both routes below with the same settings as HT ISO Ripper. When either is configured it:

1. signs HTREASURE's own binaries in the self-contained build (`HTBulkFileRenamer.exe`/`.dll` and the `HTREASURE.*`
   libraries). The .NET runtime files are already signed by Microsoft and are left as they are;
2. signs the Setup program and its uninstaller (Inno Setup `SignTool` + `SignedUninstaller`);
3. builds the portable zip from the signed files;
4. verifies every signature with `Get-AuthenticodeSignature` and stops if any isn't valid.

All signatures use SHA-256 and an RFC 3161 timestamp, so they stay valid after the certificate expires.
Without signing configured, the script builds an **unsigned** release and says so.

## Route B (preferred): Azure Artifact Signing

Microsoft's managed signing service (formerly *Trusted Signing*). Microsoft holds the key, so there is no hardware
token, and it works in GitHub Actions. Microsoft lists about $9.99 a month (Basic) and needs a **paid** Azure
subscription.

> **Check eligibility first.** Microsoft lists United Kingdom organisations as eligible, but its MSIX signing page
> says organisations need **three or more years of verifiable tax history**, and Microsoft Q&A threads report UK
> companies failing validation for that reason and new non-US/Canada organisations being paused during the preview.
> HTREASURE LTD was incorporated on 24 April 2026. Ask Azure Support before paying; if it isn't eligible yet, use
> Route A.

1. Create an Azure subscription for HTREASURE LTD, then an **Artifact Signing account** in the Azure portal. Note the
   region **endpoint**, for example `https://weu.codesigning.azure.net/`.
2. Complete **organisation identity validation** (company details must match Companies House). Allow a few business days.
3. Create a **Public Trust certificate profile**, and give the signing identity the *Artifact Signing Certificate
   Profile Signer* role.
4. Install the Windows SDK (signtool) and Microsoft's signing client, which provides `Azure.CodeSigning.Dlib.dll`.
5. Build a signed release:

```bash
az login
```

```bash
powershell -Command "$env:HT_ARTIFACT_SIGNING_ENDPOINT='https://weu.codesigning.azure.net/'; $env:HT_ARTIFACT_SIGNING_ACCOUNT='<account>'; $env:HT_ARTIFACT_SIGNING_PROFILE='<profile>'; $env:HT_ARTIFACT_SIGNING_DLIB='<path>\Azure.CodeSigning.Dlib.dll'; .\Build-Release.ps1"
```

In GitHub Actions, sign in with a service principal by setting `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` and
`AZURE_CLIENT_SECRET` as secrets.

## Route A: OV or EV certificate from a certificate authority

Buy an Organisation Validation (OV) or Extended Validation (EV) code-signing certificate for HTREASURE LTD from an
authority such as DigiCert, Sectigo or GlobalSign. The private key must be kept on a hardware token or HSM (or the
authority's cloud signing service). Install the token's driver so the certificate appears in the Windows certificate
store, then:

```bash
powershell -Command "$env:HT_SIGN_THUMBPRINT='<certificate thumbprint>'; $env:HT_TIMESTAMP_URL='<the authority timestamp URL>'; .\Build-Release.ps1"
```

Certificates on hardware tokens can't be used from GitHub-hosted runners; sign on the PC that holds the token.

## Interim: self-signed certificate (local file)

For internal and test PCs only. Create the certificate once:

```powershell
.\Tools\New-SelfSignedCodeSigningCert.ps1
```

This writes, to `C:\HTSoftware\signing` (outside the repository):

- `HTREASURE-CodeSigning.pfx`: certificate and private key, protected by a random password. Keep it private and back it up.
- `HTREASURE-CodeSigning.pfx.password`: that password, DPAPI-encrypted so only the same Windows account on the same PC
  can read it. It is never typed or shown. On another PC, create a new certificate there.
- `HTREASURE-CodeSigning.cer`: the public certificate, safe to share.

Build:

```powershell
$env:HT_SIGN_PFX='C:\HTSoftware\signing\HTREASURE-CodeSigning.pfx'; $env:HT_TIMESTAMP_URL='http://timestamp.digicert.com'; .\Build-Release.ps1
```

The script notices that the certificate issued itself, accepts Windows' "untrusted root" result, still checks that every
file was signed by that exact certificate, and copies the `.cer` into the output folder. On a test PC you control, an
administrator can trust it with `Import-Certificate -FilePath HTREASURE-CodeSigning.cer -CertStoreLocation
Cert:\LocalMachine\TrustedPublisher` (and `Cert:\LocalMachine\Root`). Never ask customers to do this.

## Not the same thing: the advisory snapshot key

The "unsigned advisory snapshot" note in **Security Advisory History** is about HTREASURE's own advisory-feed key from
the software standards (`ht-standards keygen`), passed with `-SigningKey`/`-KeyId`. A code-signing certificate does
not replace it.

## Checking a signed release

```bash
signtool verify /pa /v HTBulkFileRenamer-0.7.0-Setup.exe
```

Or: right-click the file → Properties → Digital Signatures → *HTREASURE LTD*. SmartScreen reputation builds over time,
even for signed files.

## Sources

- [Code signing options for Windows app developers](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options) (eligibility, price)
- [Quickstart: Set up Artifact Signing](https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart)
- [Sign an MSIX package](https://learn.microsoft.com/en-us/windows/msix/package/signing-package-overview) (three-year history)
