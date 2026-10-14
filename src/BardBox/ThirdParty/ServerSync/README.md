# Embedded ServerSync

Source: https://github.com/blaxxun-boop/ServerSync/blob/c57c2aa/ConfigSync.cs
Revision: c57c2aa (upstream v1.20 source revision)
Author: Tykea / blaxxun-boop
License: LICENSE.txt in this directory; copied to release packages as ServerSync-LICENSE.txt.

Local changes: removed the JetBrains.Annotations import and build-only PublicAPI / UsedImplicitly annotations. No synchronization logic changes.

The source is compiled into BardBox.dll. It is not a separate required plugin. It supplies configuration metadata recognized by the installed ConfigurationManager.
