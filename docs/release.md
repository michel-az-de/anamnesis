# Release canonico do instalador Windows

## Fonte unica de versao

Edite somente [`release/versao.json`](../release/versao.json). Ele declara a versao apresentada ao usuario, a versao numerica de quatro componentes usada pelo Windows e a URL e o SHA-256 da release anterior usada no smoke de atualizacao.

Uma release deve receber exatamente a tag indicada pela propriedade `Tag` derivada dessa configuracao.

## Gerar e verificar localmente

```powershell
$versao = Get-Content .\release\versao.json -Raw | ConvertFrom-Json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Installer.ps1
$raiz = "artifacts\releases\$($versao.Versao)\installer"
Get-Content "$raiz\SHA256SUMS.txt"
Get-FileHash "$raiz\Anamnesis-$($versao.Versao)-win-x64-setup.exe" -Algorithm SHA256
```

O resultado contem apenas um pacote canonico em `artifacts/releases/<versao>/installer/`:

- `Anamnesis-<versao>-win-x64-setup.exe`
- `SHA256SUMS.txt`
- `release.json`, com versao, commit, arquivo e hash

Os binarios e payloads sao ignorados pelo Git. Uma compilacao local ou de branch e candidata. O pacote oficial deve ser obtido dos assets imutaveis da GitHub Release ou do espelho byte a byte no GitLab Generic Package Registry. Uma recompilacao da tag nunca substitui esses bytes.

O build padrao recusa uma arvore Git suja. `-PermitirArvoreDeTrabalhoSuja` existe apenas para diagnostico local e registra `arvoreDeTrabalhoLimpa: false` no manifesto, portanto nao deve ser usado para distribuir uma release.

Para conferir a release anterior configurada sem executar o instalador:

```powershell
$anterior = powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Obter-InstaladorAnterior.ps1
Get-FileHash $anterior -Algorithm SHA256
```

O script baixa somente por HTTPS, tenta novamente falhas transitórias, valida o SHA-256 antes de concluir o arquivo e reaproveita o cache apenas se o hash continuar correto.

## GitHub Actions

Pull requests, `main` e execucoes manuais podem gerar um artifact temporario prefixado por `candidato-`. O fluxo baixa a release anterior oficial, constroi a versao atual uma unica vez e executa instalacao, reparo, atualizacao, bloqueio de downgrade e desinstalacao.

Somente uma tag exata `v<versao>` promove os tres arquivos para uma GitHub Release. A opcao **Immutable releases** deve permanecer ativa nas configuracoes do repositorio, como ja esta neste projeto. O job recusa uma release existente e valida versao, tag, commit completo, arvore limpa e hash. Imediatamente depois da criacao, exige `isImmutable: true`; se a configuracao tiver sido desativada, remove automaticamente somente a release mutavel, preserva a tag e falha. Com a protecao confirmada, executa `gh release verify` e `gh release verify-asset`.

A release-base [`v0.2.0-beta.1`](https://github.com/michel-az-de/anamnesis/releases/tag/v0.2.0-beta.1) foi promovida do run `31086786207` e possui atestado imutavel. Seu instalador tem SHA-256 `5852e3e82ab9c80cf72ab85b8cd4425aeab3aa6faf401b3da0a2fc3db23dbecd`.

## GitLab CI

O GitLab nao recompila nem executa o instalador. Depois que a GitHub Release da mesma tag estiver disponivel, ele valida a fonte, baixa EXE, `SHA256SUMS.txt` e `release.json`, confirma commit e hashes e publica exatamente os mesmos bytes no Generic Package Registry.

Configure um runner dedicado com:

- tag `windows-release`;
- PowerShell 7 e .NET 10;
- opcoes **Protected** e execucao apenas de jobs com tag;
- maquina efemera ou descartada apos o job;
- sem permissao para pipelines de merge request.

A pipeline aceita somente tag Git protegida e usa `CI_JOB_TOKEN`. Configure **Allow duplicates: false** no Generic Package Registry. O job nao e interrompivel: se uma tentativa anterior tiver publicado apenas parte dos assets, ele reutiliza somente arquivos com hash identico, completa os ausentes e baixa novamente os tres para verificar o espelho.

## Publicar uma tag

Depois do merge em `main`, atualize `release/versao.json`, revise e publique primeiro no GitHub:

```powershell
$versao = Get-Content .\release\versao.json -Raw | ConvertFrom-Json
$tag = "v$($versao.versao)"
git tag -a $tag -m "Anamnesis $($versao.versao)"
git push origin $tag
gh release verify $tag --repo michel-az-de/anamnesis
```

Somente depois de `gh release verify` ficar verde, envie a mesma tag e o mesmo commit ao GitLab:

```powershell
git push <remote-gitlab> $tag
```

Substitua `<remote-gitlab>` por um remoto configurado e autorizado. Proteja previamente o padrao de tag `v*` e o runner `windows-release`. A pipeline rejeita tag divergente, desprotegida ou sem a GitHub Release oficial correspondente.
