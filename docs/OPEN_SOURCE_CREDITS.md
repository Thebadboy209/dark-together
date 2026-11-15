# Open-source credits

The original code inside this starter package (`server/src/*`, `unity-client/Assets/Scripts/*`,
`unity-client/Assets/Editor/*`) was authored for this prototype. The dependency listed below
is installed separately via its public package manager and **is not vendored/copied** into this zip.

| Project | Repository | License | Actual reuse |
|--|--|--|--|
| Colyseus server | https://github.com/colyseus/colyseus | MIT | Room matchmaking, server API and state sync |
| Colyseus Unity SDK | https://github.com/colyseus/colyseus-unity-sdk | MIT | Client room connection and DynamicSchema |
| @colyseus/schema | https://github.com/colyseus/schema | MIT | Server state serialization |

Other codebases previously considered (not included):
- https://github.com/mhhung0811/Project-2TDS
- https://github.com/Unity-Technologies/2d-techdemos

They were not merged into the prototype: that would have introduced version and licensing integration
steps before confirming the simple networked prototype works. You can integrate compatible modules
later after checking each repository's license and original copyright notices.

Game visual placeholders are generated at runtime from original C# code, not copied from third-party games.
