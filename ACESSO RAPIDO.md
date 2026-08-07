## Logins verificados por requisição

Testei os 12 no `POST /api/auth/login`:

|Login|Perfil|Senha|Resultado|
|---|---|---|---|
|`EJVESSIO`|Master|`Lumina2026!`|200 ✓|
|`DEMO`|Master (demo)|`Demo2026!`|200 ✓|
|`GESTOR01`|Gestão|`Erplumina1234!`|200 ✓|
|`LIDER01` / `LIDER02`|Líder|`Erplumina1234!`|200 ✓|
|`PADRAO01` / `PADRAO02` / `PADRAO03`|Padrão|`Erplumina1234!`|200 ✓|
|`RC01` / `RC02`|RC (externo)|`Erplumina1234!`|200 ✓|
|`AUDITOR01`|Auditoria|`Erplumina1234!`|200 ✓|
|`SISTEMA`|Padrão (service account)|`Erplumina1234!`|**401 — por desenho**|
