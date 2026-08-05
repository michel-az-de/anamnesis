---
title: Plano de Ambiente de Desenvolvimento
aliases: [Bootstrap local, Plano Docker e Kubernetes]
tags: [operacao, desenvolvimento, ambiente-local]
type: runbook
created: 2026-08-05
updated: 2026-08-05
status: aprovado
summary: Ambiente Windows local para .NET, React, Angular, PostgreSQL, Terraform e estudo pontual de Kubernetes, com limite explícito de recursos.
related: ["[[Anamnesis Home]]", "[[Protocolo de Agentes]]", "[[Roteiro de Validacao Alpha]]"]
---

# Plano de ambiente de desenvolvimento

## Decisão

Usar **Docker Desktop com backend WSL 2** para os serviços locais e **kind sob demanda** para estudar Kubernetes. Não habilitar um cluster Kubernetes permanente no Docker Desktop.

Isso mantém PostgreSQL e dependências reproduzíveis por Docker Compose, mas só consome os recursos adicionais de Kubernetes durante uma sessão de estudo. `kind` precisa do Docker e cria clusters locais descartáveis; ao terminar, o cluster deve ser removido.

## Diagnóstico de referência

| Recurso | Estado em 2026-08-05 | Impacto |
| --- | --- | --- |
| CPU | Intel i5-12400F, 6 núcleos / 12 threads | Adequado para Docker e um cluster local de estudo. |
| RAM | 31,8 GB | Adequada se o WSL for limitado. |
| Disco C: | 930,6 GB, 801,7 GB livres | Sem restrição relevante. |
| WSL | padrão WSL 2, sem distribuição; não inicia | Bloqueador inicial: habilitar Plataforma de Máquina Virtual e confirmar virtualização no firmware se necessário. |

## Política de recursos

Criar `%UserProfile%\.wslconfig` depois de habilitar o WSL:

```ini
[wsl2]
memory=10GB
processors=6
swap=2GB

[experimental]
autoMemoryReclaim=gradual
```

O teto vale para o WSL, Docker e `kind` juntos. Ele preserva mais de 20 GB para Windows, Visual Studio, navegador e ferramentas de IA. Para uma sessão pesada de Kubernetes, fechar containers não relacionados; não aumentar o limite como solução inicial.

## Ferramentas aprovadas

| Grupo | Ferramenta | Decisão | Critério de validação |
| --- | --- | --- | --- |
| IDE .NET | Visual Studio Professional 2026 | Instalar com workloads .NET desktop e ASP.NET | Abre `Anamnesis.sln` e executa testes. |
| SDK | .NET SDK 10 | Instalar | `dotnet --info` mostra SDK 10. |
| Containers | Docker Desktop + Ubuntu no WSL 2 | Instalar após o reinício do WSL | `docker run hello-world` funciona. |
| Banco local | PostgreSQL em Docker Compose | Usar por projeto; não instalar servidor global | volume persiste e conexão funciona. |
| Frontend | Node.js LTS | Instalar | `node --version` e `npm --version`. |
| IaC | Terraform | Instalar para estudo | `terraform --version`. |
| Kubernetes | kubectl e kind | Instalar; criar cluster apenas sob demanda | `kubectl cluster-info` em um cluster de estudo. |
| Fluxo diário | PowerShell 7, GitHub CLI, Bruno e DBeaver Community | Instalar | cada CLI abre/retorna versão; GUI abre. |
| Nuvem | Azure CLI | Manter a instalação existente | localizar e validar `az version` após reiniciar o terminal. |

## Sequência de execução

1. **Desbloquear virtualização.** Executar `wsl --install --no-distribution` como administrador e reiniciar. Se o WSL continuar indicando virtualização desabilitada, habilitar Intel Virtualization Technology/VT-x na UEFI e reiniciar novamente.
2. **Instalar o núcleo de desenvolvimento.** Visual Studio Professional, .NET 10, Node.js LTS, PowerShell 7, GitHub CLI, Bruno, DBeaver e Terraform.
3. **Preparar Linux e containers.** Instalar Ubuntu no WSL 2, aplicar `.wslconfig`, desligar o WSL (`wsl --shutdown`) e instalar/configurar Docker Desktop com integração à Ubuntu.
4. **Validar PostgreSQL por Compose.** Usar um `compose.yaml` no repositório do projeto; credenciais ficam em `.env` ignorado pelo Git. Não expor a porta do banco além de `localhost` sem necessidade.
5. **Habilitar estudo de Kubernetes.** Instalar `kubectl` e `kind`, criar um único cluster somente durante o estudo: `kind create cluster --name estudos`. Remover ao fim: `kind delete cluster --name estudos`.
6. **Revisar consumo.** Antes de aumentar memória, usar `docker stats`, `wsl --status` e o Gerenciador de Tarefas. O limite padrão de 10 GB é a proteção principal.

## Registro de execução

| Data | Ação | Resultado |
| --- | --- | --- |
| 2026-08-05 | `wsl --install --no-distribution` | Concluído; requer reinicialização para aplicar os recursos do WSL. |
| 2026-08-05 | Validação pós reinicialização | WSL 2 e Docker Desktop ativos; `docker run --rm hello-world` concluído. |
| 2026-08-05 | Limites do WSL | `%UserProfile%\.wslconfig` aplicado com 10 GB, 6 CPUs, 2 GB de swap e recuperação gradual de memória. |
| 2026-08-05 | Ubuntu | Instalado como distribuição WSL padrão; a criação inicial da conta Linux ainda depende do usuário. |
| 2026-08-05 | Verificação do .NET SDK 10 | O WinGet o reconhece como instalado; validar `dotnet --info` em um terminal novo após reiniciar. |
| 2026-08-05 | Node.js LTS | Instalado em escopo do usuário. |
| 2026-08-05 | Terraform, kind, PowerShell 7, Bruno e DBeaver Community | Instalados em escopo do usuário. |
| 2026-08-05 | Visual Studio Professional 2026 | Bootstrapper oficial com hash validado e instalador gráfico aberto com workloads .NET Desktop e ASP.NET. Autenticação da licença e conclusão pendentes. |

### Comandos de retomada (PowerShell como Administrador)

```powershell
winget install --id Microsoft.VisualStudio.Professional --exact --source winget --accept-package-agreements --accept-source-agreements
winget install --id OpenJS.NodeJS.LTS --exact --source winget --accept-package-agreements --accept-source-agreements
winget install --id Docker.DockerDesktop --exact --source winget --accept-package-agreements --accept-source-agreements
winget install --id Hashicorp.Terraform --exact --source winget --accept-package-agreements --accept-source-agreements
winget install --id Kubernetes.kubectl --exact --source winget --accept-package-agreements --accept-source-agreements
winget install --id Kubernetes.kind --exact --source winget --accept-package-agreements --accept-source-agreements
winget install --id Microsoft.PowerShell --exact --source winget --accept-package-agreements --accept-source-agreements
winget install --id GitHub.cli --exact --source winget --accept-package-agreements --accept-source-agreements
winget install --id Bruno.Bruno --exact --source winget --accept-package-agreements --accept-source-agreements
winget install --id DBeaver.DBeaver.Community --exact --source winget --accept-package-agreements --accept-source-agreements
```

## Regras para agentes (Codex e Claude)

- Ler este plano antes de instalar, atualizar ou depurar ferramentas do ambiente.
- Não habilitar Kubernetes permanente no Docker Desktop sem uma necessidade de projeto registrada.
- Não criar cluster `kind` com mais de um nó sem pedido explícito.
- Preferir Docker Compose para dependências de desenvolvimento, inclusive PostgreSQL.
- Registrar neste documento qualquer mudança de teto de recursos, ferramenta substituída ou falha de pré-requisito, com data e motivo.
- Não colocar segredos, tokens, senhas ou arquivos `.env` neste repositório.

## Critérios de aceite

- `dotnet test Anamnesis.sln --configuration Release --no-restore --verbosity minimal` executa localmente.
- Um projeto React e um Angular iniciam com Node LTS.
- PostgreSQL sobe e persiste dados via Docker Compose.
- `terraform fmt -check` e `terraform validate` funcionam em um exemplo local.
- Um cluster `kind` de um nó é criado, recebe um deployment de teste e é removido sem deixar containers Kubernetes em execução.

## Referências oficiais

- [WSL: configuração avançada](https://learn.microsoft.com/pt-br/windows/wsl/wsl-config)
- [Docker Desktop: Kubernetes local](https://docs.docker.com/desktop/use-desktop/kubernetes/)
- [Kubernetes: ferramentas locais](https://kubernetes.io/docs/tasks/tools/)
