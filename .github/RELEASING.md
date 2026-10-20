# Releasing

## One-time signing setup

Create a service account in the Unity organization that should sign the package.
Give it the **Package Manager Package Signer** organization role, then add these
repository secrets:

- `UPM_SERVICE_ACCOUNT_KEY_ID`
- `UPM_SERVICE_ACCOUNT_KEY_SECRET`
- `UPM_ORG_ID`

See [Unity's UPM CLI setup](https://docs.unity3d.com/6000.6/Documentation/Manual/upm-cli-install.html#prereqs).
The existing `UNITY_EMAIL` and `UNITY_PASSWORD` secrets remain the credentials
for the Editor license used by the example build and `.unitypackage` export.

Before the next version tag is created, merge these settings into the package's
[OpenUPM metadata](https://github.com/openupm/openupm/blob/master/data/packages/com.mackysoft.serializereference-extensions.yml):

```yaml
trackingMode: githubRelease
githubReleaseAssetName: com.mackysoft.serializereference-extensions-
```

This order prevents OpenUPM from publishing an unsigned archive packed from Git.
Existing published versions remain unchanged. See [OpenUPM's signing workflow](https://openupm.com/docs/signing-upm-packages).

## Release a version

1. Run **Release** from a branch with the new SemVer tag. It updates `package.json`,
   builds the example, exports the `.unitypackage`, and signs the UPM `.tgz` from
   the same commit. Signing must succeed before the tag and draft Release are created.
2. Review the draft's release notes and attached packages, then publish it.
3. The **Release / publish-openupm** job requests publication through GitHub OIDC
   and checks that OpenUPM reports the version as signed. No OpenUPM token is needed.

Both release assets use `<package.json name>-<version>.<extension>`, for example:

- `com.mackysoft.serializereference-extensions-2.0.1.unitypackage`
- `com.mackysoft.serializereference-extensions-2.0.1.tgz`

Keep the signed `.tgz` attached to the public Release so OpenUPM can retrieve it.
