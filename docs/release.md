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

Os binarios e payloads sao ignorados pelo Git. Uma compilacao local ou de branch e candidata. O pacote oficial deve ser obtido dos assets imutaveis da GitHub Release. Uma recompilacao da tag nunca substitui esses bytes.

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

## Publicar uma tag

Depois do merge em `main`, atualize `release/versao.json`, revise e publique no GitHub:

```powershell
$versao = Get-Content .\release\versao.json -Raw | ConvertFrom-Json
$tag = "v$($versao.versao)"
git tag -a $tag -m "Anamnesis $($versao.versao)"
git push origin $tag
gh release verify $tag --repo michel-az-de/anamnesis
```
